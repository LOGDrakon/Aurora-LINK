#!/usr/bin/env python3
"""
Aurora-LINK Device Simulator
=============================

Simulates an Aurora LED module over a virtual serial port, speaking the LINK
protocol exactly as the real STM32G0B1 firmware would.

To create a pair of virtual COM ports you can use:
  - Windows : com0com  (e.g. COM10 <-> COM11)
  - Linux   : socat    (socat -d -d pty,raw,echo=0 pty,raw,echo=0)

The simulator connects to one end of the pair; point Aurora-LINK at the other.

The device starts in LOCKED state.  Once the client authenticates (AUTH),
the simulator sends a single GETINPUT frame with the current input states.
After that, each console input change (Ix=ON/OFF) pushes a new GETINPUT.

Supported LINK frames (received from client):
  LINK:GETAPP\0
  LINK:AURORA:GETV\0
  LINK:AURORA:AUTH:<password>\0
  LINK:AURORA:PING\0
  LINK:AURORA:UPLOAD:START:<size>\0     -> OK (ready to receive)
  LINK:AURORA:UPLOAD:DATA:<seq>:<hex>\0 -> OK (chunk received, max 64 raw bytes)
  LINK:AURORA:UPLOAD:END\0              -> OK or ERR (integrity check)
  LINK:AURORA:<any>\0                   -> ERR:UNKNOWN_COMMAND

Pushed frames (sent to client after connection):
  LINK:AURORA:GETINPUT:<I0><I1>...<I9>\0
    Each <Ix> is '0' (OFF) or '1' (ON).

Interactive console commands:
  I0=ON    / I0=OFF      Toggle a single input (I0..I9)
  I3=1     / I3=0        Same, numeric form
  STATUS                 Print current input states
  QUIT                   Stop the simulator

Usage:
  python aurora_link_simulator.py
  python aurora_link_simulator.py --port COM11 --password secret

Requirements:
  pip install pyserial
"""

import argparse
import binascii
import sys
import threading
from dataclasses import dataclass, field

try:
    import serial
except ImportError:
    print(
        "pyserial is required.  Install it with:\n  pip install pyserial",
        file=sys.stderr,
    )
    sys.exit(1)


# ---------------------------------------------------------------------------
# Device state
# ---------------------------------------------------------------------------

INPUT_COUNT = 10

@dataclass
class DeviceState:
    app_id: str = "AURORA"
    link_version: str = "LINKv1.1"
    uid: str = "0xAUR00001"
    model: str = "Aurora-LED"
    enc: str = "NONE"
    locked: bool = True
    connected: bool = False
    password: str = "aurora"
    inputs: list = field(default_factory=lambda: [False] * INPUT_COUNT)
    upload_buffer: bytearray = field(default_factory=bytearray)
    upload_expected_size: int = 0
    upload_seq: int = 0

    def getv_args(self) -> list:
        return [
            self.link_version,
            f"UID={self.uid}",
            f"MODEL={self.model}",
            f"ENC={self.enc}",
            f"LOCKED={'true' if self.locked else 'false'}",
        ]

    def input_payload(self) -> str:
        """Returns e.g. '0100000000' for the 10 inputs."""
        return "".join("1" if v else "0" for v in self.inputs)


# ---------------------------------------------------------------------------
# Frame helpers
# ---------------------------------------------------------------------------

def build_frame(app_id: str | None, command: str, *args) -> bytes:
    parts = ["LINK"]
    if app_id:
        parts.append(app_id)
    parts.append(command)
    parts.extend(args)
    return (":".join(parts) + "\0").encode("latin-1")


def parse_frame(raw: str) -> dict:
    if not raw.strip():
        raise ValueError("empty frame")

    parts = [p for p in raw.split(":") if p != ""]
    if len(parts) < 2 or parts[0] != "LINK":
        raise ValueError(f"invalid LINK frame: {raw!r}")

    if len(parts) == 2 and parts[1] == "GETAPP":
        return {"app_id": None, "command": "GETAPP", "args": []}

    if len(parts) < 3:
        raise ValueError(f"incomplete standard frame: {raw!r}")

    return {
        "app_id": parts[1],
        "command": parts[2],
        "args": parts[3:],
    }


# ---------------------------------------------------------------------------
# .flora integrity verification
# ---------------------------------------------------------------------------

def verify_flora(data: bytearray) -> bool:
    """
    Verify a .flora binary: check minimum size, FLOR signature and CRC-32.

    Layout:  Header(16) + TLV(...) + CRC32(4) + Signature(4)
    CRC-32 (IEEE 802.3) is computed over everything before the CRC32 field.
    """
    min_size = 16 + 4 + 4  # header + crc + signature
    if len(data) < min_size:
        return False
    # Signature "FLOR" at end (little-endian uint32 0x464C4F52 → bytes 52 4F 4C 46)
    signature = (0x464C4F52).to_bytes(4, byteorder="little")
    if data[-4:] != signature:
        return False
    # CRC-32 stored just before the signature
    stored_crc = int.from_bytes(data[-8:-4], byteorder="little")
    computed_crc = binascii.crc32(data[:-8]) & 0xFFFFFFFF
    return stored_crc == computed_crc


# ---------------------------------------------------------------------------
# Serial handler
# ---------------------------------------------------------------------------

class SerialHandler:
    def __init__(self, ser: serial.Serial, state: DeviceState,
                 verbose: bool = True):
        self.ser = ser
        self.state = state
        self.verbose = verbose
        self._buf = bytearray()
        self._running = False
        self._lock = threading.Lock()

    def log(self, msg: str):
        if self.verbose:
            print(f"[AURORA-SIM] [{self.ser.port}] {msg}",
                  file=sys.stderr, flush=True)

    def send(self, payload: bytes):
        with self._lock:
            self.ser.write(payload)
        self.log(f"TX {payload!r}")

    def send_input_state(self):
        """Push current input states to the client."""
        if not self.state.connected:
            return
        self.send(build_frame(
            self.state.app_id, "GETINPUT", self.state.input_payload()
        ))

    def handle_frame(self, raw: str):
        self.log(f"RX {raw!r}")
        try:
            frame = parse_frame(raw)
        except ValueError as exc:
            self.log(f"Ignored invalid frame: {exc}")
            return

        app_id = frame["app_id"]
        command = frame["command"]
        args = frame["args"]
        state = self.state

        # --- GETAPP (no app-id) ---
        if command == "GETAPP":
            self.send(build_frame(state.app_id, "RETURN", "GETAPP",
                                  state.app_id))
            return

        # Only respond if app-id matches
        if app_id != state.app_id:
            self.log(f"Ignored command for unknown app_id={app_id!r}")
            return

        # --- GETV ---
        if command == "GETV":
            self.send(build_frame(state.app_id, "RETURN", "GETV",
                                  *state.getv_args()))
            return

        # --- AUTH ---
        if command == "AUTH":
            supplied = args[0] if args else ""
            if supplied == state.password:
                state.locked = False
                state.connected = True
                self.send(build_frame(state.app_id, "RETURN", "AUTH", "OK"))
                self.log("Device UNLOCKED — client connected")
                # Send initial input state
                self.send_input_state()
            else:
                self.send(build_frame(state.app_id, "RETURN", "AUTH", "ERR"))
                self.log(f"AUTH failed (got {supplied!r})")
            return

        # --- PING ---
        if command == "PING":
            self.send(build_frame(state.app_id, "RETURN", "PING", "PONG"))
            return

        # --- UPLOAD ---
        if command == "UPLOAD":
            self._handle_upload(args)
            return

        # --- Unknown ---
        self.send(build_frame(state.app_id, "RETURN", command,
                              "ERR", "UNKNOWN_COMMAND"))

    def _handle_upload(self, args: list):
        """Handle UPLOAD sub-commands: START, DATA, END."""
        state = self.state
        app = state.app_id

        if not args:
            self.send(build_frame(app, "RETURN", "UPLOAD",
                                  "ERR", "MISSING_SUBCOMMAND"))
            return

        sub = args[0]

        # --- UPLOAD:START:<size> ---
        if sub == "START":
            if len(args) < 2:
                self.send(build_frame(app, "RETURN", "UPLOAD",
                                      "ERR", "MISSING_SIZE"))
                return
            try:
                expected = int(args[1])
            except ValueError:
                self.send(build_frame(app, "RETURN", "UPLOAD",
                                      "ERR", "INVALID_SIZE"))
                return
            state.upload_buffer = bytearray()
            state.upload_expected_size = expected
            state.upload_seq = 0
            self.log(f"UPLOAD START — expecting {expected} bytes")
            self.send(build_frame(app, "RETURN", "UPLOAD", "OK"))
            return

        # --- UPLOAD:DATA:<seq>:<hex> ---
        if sub == "DATA":
            if len(args) < 3:
                self.send(build_frame(app, "RETURN", "UPLOAD",
                                      "ERR", "MISSING_DATA"))
                return
            try:
                seq = int(args[1])
            except ValueError:
                self.send(build_frame(app, "RETURN", "UPLOAD",
                                      "ERR", "INVALID_SEQ"))
                return
            if seq != state.upload_seq:
                self.send(build_frame(app, "RETURN", "UPLOAD",
                                      "ERR", "SEQ_MISMATCH"))
                return
            try:
                chunk = bytes.fromhex(args[2])
            except ValueError:
                self.send(build_frame(app, "RETURN", "UPLOAD",
                                      "ERR", "INVALID_HEX"))
                return
            state.upload_buffer.extend(chunk)
            state.upload_seq += 1
            self.log(f"UPLOAD DATA seq={seq} +{len(chunk)}B "
                     f"(total={len(state.upload_buffer)}"
                     f"/{state.upload_expected_size})")
            self.send(build_frame(app, "RETURN", "UPLOAD", "OK"))
            return

        # --- UPLOAD:END ---
        if sub == "END":
            received = len(state.upload_buffer)
            expected = state.upload_expected_size
            if received != expected:
                self.log(f"UPLOAD END — SIZE MISMATCH "
                         f"({received} != {expected})")
                self.send(build_frame(app, "RETURN", "UPLOAD",
                                      "ERR", "SIZE_MISMATCH"))
            elif not verify_flora(state.upload_buffer):
                self.log("UPLOAD END — INTEGRITY CHECK FAILED")
                self.send(build_frame(app, "RETURN", "UPLOAD",
                                      "ERR", "INTEGRITY"))
            else:
                self.log(f"UPLOAD END — OK ({received} bytes, "
                         f"integrity verified)")
                self.send(build_frame(app, "RETURN", "UPLOAD", "OK"))
            # Reset upload state
            state.upload_buffer = bytearray()
            state.upload_expected_size = 0
            state.upload_seq = 0
            return

        self.send(build_frame(app, "RETURN", "UPLOAD",
                              "ERR", "UNKNOWN_SUBCOMMAND"))

    def run(self):
        self._running = True
        self.log("Waiting for data...")
        try:
            while self._running:
                data = self.ser.read(1)
                if not data:
                    continue
                waiting = self.ser.in_waiting
                if waiting:
                    data += self.ser.read(waiting)

                for byte in data:
                    if byte == 0:
                        raw = self._buf.decode("ascii", errors="ignore")
                        self._buf.clear()
                        self.handle_frame(raw)
                    else:
                        self._buf.append(byte)
        except serial.SerialException as exc:
            self.log(f"Serial error: {exc}")
        except Exception as exc:
            self.log(f"Error: {exc}")
        finally:
            self.log("Handler stopped.")

    def stop(self):
        self._running = False


# ---------------------------------------------------------------------------
# Interactive console
# ---------------------------------------------------------------------------

def run_console(handler: SerialHandler, state: DeviceState):
    """
    Reads stdin for commands like:
      I0=ON   I3=OFF   I7=1   I2=0   STATUS   QUIT
    """
    print_status(state)
    print("\nCommandes: I0=ON, I3=OFF, I7=1, STATUS, QUIT\n")

    while True:
        try:
            line = input("aurora-sim> ").strip()
        except (EOFError, KeyboardInterrupt):
            break

        if not line:
            continue

        upper = line.upper()

        if upper == "QUIT":
            break

        if upper == "STATUS":
            print_status(state)
            continue

        # Parse Ix=ON/OFF/1/0
        if "=" in upper and upper[0] == "I":
            try:
                left, right = upper.split("=", 1)
                idx = int(left[1:])
                if idx < 0 or idx >= INPUT_COUNT:
                    print(f"  Erreur: index {idx} hors plage (0-{INPUT_COUNT - 1})")
                    continue

                if right in ("ON", "1"):
                    value = True
                elif right in ("OFF", "0"):
                    value = False
                else:
                    print(f"  Erreur: valeur '{right}' invalide (ON/OFF/1/0)")
                    continue

                state.inputs[idx] = value
                tag = "ON " if value else "OFF"
                print(f"  I{idx} -> {tag}")

                # Push to client if connected
                if state.connected:
                    handler.send_input_state()
                else:
                    print("  (non connecté — GETINPUT non envoyé)")
                continue
            except ValueError:
                pass

        print(f"  Commande inconnue: {line}")
        print("  Commandes: I0=ON, I3=OFF, I7=1, STATUS, QUIT")


def print_status(state: DeviceState):
    """Pretty-print the 10 input states."""
    header = "  ".join(f"I{i}" for i in range(INPUT_COUNT))
    values = "  ".join(
        (" \033[92mON\033[0m" if v else "\033[90mOFF\033[0m")
        for v in state.inputs
    )
    print(f"\n  {header}")
    print(f"  {values}")
    print(f"  Payload  : {state.input_payload()}")
    print(f"  Locked   : {state.locked}")
    print(f"  Connecté : {state.connected}")


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(
        description="Aurora-LINK device simulator — simulates an Aurora LED "
                    "module over a virtual serial port."
    )
    parser.add_argument("--port", default="COM10",
                        help="Serial port name (default: COM10)")
    parser.add_argument("--baud", type=int, default=115200,
                        help="Baud rate (default: 115200)")
    parser.add_argument("--password", default="aurora",
                        help="AUTH password (default: aurora)")
    parser.add_argument("--model", default="Aurora-LED",
                        help="Device model name (default: Aurora-LED)")
    parser.add_argument("--uid", default="0xAUR00001",
                        help="Device UID (default: 0xAUR00001)")
    parser.add_argument("--quiet", action="store_true",
                        help="Suppress verbose frame logging")
    args = parser.parse_args()

    state = DeviceState(
        password=args.password,
        model=args.model,
        uid=args.uid,
    )

    ser = serial.Serial(
        port=args.port,
        baudrate=args.baud,
        bytesize=8,
        parity="N",
        stopbits=1,
        timeout=0.5,
    )

    handler = SerialHandler(ser, state, verbose=not args.quiet)

    print(
        f"[AURORA-SIM] Simulateur Aurora sur {ser.port} "
        f"({ser.baudrate} 8N1)  app-id={state.app_id}",
        file=sys.stderr, flush=True,
    )
    print(
        f"[AURORA-SIM] Appareil VERROUILLÉ — mot de passe: {state.password!r}",
        file=sys.stderr, flush=True,
    )

    # Start serial handler in background thread
    serial_thread = threading.Thread(target=handler.run, daemon=True)
    serial_thread.start()

    # Interactive console on main thread
    try:
        run_console(handler, state)
    except KeyboardInterrupt:
        print("\n[AURORA-SIM] Interruption.", file=sys.stderr)
    finally:
        handler.stop()
        if ser.is_open:
            ser.close()
        print("[AURORA-SIM] Arrêté.", file=sys.stderr, flush=True)


if __name__ == "__main__":
    main()
