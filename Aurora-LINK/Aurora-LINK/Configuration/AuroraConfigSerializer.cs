using System;
using System.Buffers.Binary;
using System.IO;

namespace Aurora_LINK.Configuration;

/// <summary>
/// Sérialisation et désérialisation binaire de la configuration Aurora
/// conforme au format DT-AURORA-MEM-001 Rév. C.
/// </summary>
public static class AuroraConfigSerializer
{
    private const int TlvHeaderSize = 4; // type(1) + flags(1) + length(2)
    private const int LedPayloadSize = 4;
    private const int SceneSize = 12; // id(1) + flags(1) + state(10)
    private const int LedStateSize = 10; // mode(1) + R(1) + G(1) + B(1) + t_on(2) + t_off(2) + repeat(1) + fade(1)
    private const int InputPayloadSize = 10;
    private const int SystemPayloadSize = 8;

    // ──────────────────────────── Serialize ────────────────────────────

    /// <summary>
    /// Sérialise la configuration en une page Flash de 2048 bytes.
    /// Incrémente automatiquement <see cref="AuroraHeader.WriteCount"/>.
    /// </summary>
    public static byte[] Serialize(AuroraConfiguration config)
    {
        var page = new byte[AuroraConfiguration.FlashPageSize];
        Array.Fill(page, (byte)0xFF);

        // Construire les blocs TLV dans un buffer temporaire
        byte[] tlvData;
        byte numBlocs = 0;

        using (var tlvStream = new MemoryStream())
        using (var tw = new BinaryWriter(tlvStream))
        {
            WriteLedBloc(tw, config.LedConfig);
            numBlocs++;

            WriteScenesBloc(tw, config);
            numBlocs++;

            WriteInputsBloc(tw, config);
            numBlocs++;

            WriteSystemBloc(tw, config.SystemConfig);
            numBlocs++;

            tlvData = tlvStream.ToArray();
        }

        // Mettre à jour le header
        config.Header.NumBlocs = numBlocs;
        config.Header.TotalLength = (ushort)tlvData.Length;
        config.Header.WriteCount++;

        // Écrire le header (16 bytes)
        using var ms = new MemoryStream(page);
        using var w = new BinaryWriter(ms);
        w.Write(config.Header.Magic);
        w.Write(config.Header.Version);
        w.Write(config.Header.NumBlocs);
        w.Write(config.Header.TotalLength);
        w.Write(config.Header.WriteCount);
        w.Write(config.Header.Reserved);

        // Écrire les blocs TLV
        w.Write(tlvData);

        // CRC32 sur header + TLV, placé immédiatement après
        int crcOffset = AuroraHeader.Size + tlvData.Length;
        uint crc = ComputeCrc32(page, 0, crcOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(crcOffset), crc);

        return page;
    }

    // ──────────────────────────── Deserialize ──────────────────────────

    /// <summary>
    /// Désérialise une page Flash de 2048 bytes en configuration Aurora.
    /// Retourne <c>null</c> si le magic, la version ou le CRC sont invalides.
    /// </summary>
    public static AuroraConfiguration? Deserialize(ReadOnlySpan<byte> page)
    {
        if (page.Length != AuroraConfiguration.FlashPageSize)
            return null;

        // Lire le header
        var header = new AuroraHeader
        {
            Magic = BinaryPrimitives.ReadUInt32LittleEndian(page),
            Version = page[4],
            NumBlocs = page[5],
            TotalLength = BinaryPrimitives.ReadUInt16LittleEndian(page[6..]),
            WriteCount = BinaryPrimitives.ReadUInt32LittleEndian(page[8..]),
            Reserved = BinaryPrimitives.ReadUInt32LittleEndian(page[12..]),
        };

        if (header.Magic != AuroraHeader.MagicValue)
            return null;

        if (header.Version > AuroraHeader.CurrentVersion)
            return null;

        int crcOffset = AuroraHeader.Size + header.TotalLength;
        if (crcOffset + AuroraConfiguration.CrcSize > page.Length)
            return null;

        uint computed = ComputeCrc32(page[..crcOffset]);
        uint stored = BinaryPrimitives.ReadUInt32LittleEndian(page[crcOffset..]);
        if (computed != stored)
            return null;

        // Parser les blocs TLV
        var config = new AuroraConfiguration { Header = header };
        int pos = AuroraHeader.Size;
        int endPos = AuroraHeader.Size + header.TotalLength;

        for (int i = 0; i < header.NumBlocs && pos + TlvHeaderSize <= endPos; i++)
        {
            byte type = page[pos];
            // byte flags = page[pos + 1]; // réservé
            ushort length = BinaryPrimitives.ReadUInt16LittleEndian(page[(pos + 2)..]);
            int dataStart = pos + TlvHeaderSize;

            if (dataStart + length > endPos)
                break;

            var payload = page.Slice(dataStart, length);

            switch (type)
            {
                case (byte)AuroraBlocType.Leds:
                    config.LedConfig = ParseLedConfig(payload);
                    break;
                case (byte)AuroraBlocType.Scenes:
                    ParseScenesBloc(payload, config);
                    break;
                case (byte)AuroraBlocType.Inputs:
                    ParseInputsBloc(payload, config);
                    break;
                case (byte)AuroraBlocType.System:
                    config.SystemConfig = ParseSystemConfig(payload);
                    break;
            }

            pos = dataStart + length;
        }

        return config;
    }

    // ──────────────────────── Bloc Writers ─────────────────────────────

    private static void WriteTlvHeader(BinaryWriter w, AuroraBlocType type, ushort payloadLength)
    {
        w.Write((byte)type);
        w.Write((byte)0x00); // flags
        w.Write(payloadLength);
    }

    private static void WriteLedBloc(BinaryWriter w, AuroraLedConfig led)
    {
        WriteTlvHeader(w, AuroraBlocType.Leds, LedPayloadSize);
        w.Write(led.MaxPwm);
        w.Write(led.SoftStartMs);
        w.Write(led.Reserved);
    }

    private static void WriteScenesBloc(BinaryWriter w, AuroraConfiguration config)
    {
        int scenesBytes = config.Scenes.Count * SceneSize;

        WriteTlvHeader(w, AuroraBlocType.Scenes, (ushort)scenesBytes);

        foreach (var scene in config.Scenes)
        {
            w.Write(scene.SceneId);
            w.Write((byte)scene.Flags);
            w.Write((byte)scene.State.Mode);
            w.Write(scene.State.Red);
            w.Write(scene.State.Green);
            w.Write(scene.State.Blue);
            w.Write(scene.State.TOnMs);
            w.Write(scene.State.TOffMs);
            w.Write(scene.State.Repeat);
            w.Write(scene.State.FadeTime);
        }
    }

    private static void WriteInputsBloc(BinaryWriter w, AuroraConfiguration config)
    {
        WriteTlvHeader(w, AuroraBlocType.Inputs, (ushort)(config.Inputs.Count * InputPayloadSize));

        foreach (var input in config.Inputs)
        {
            w.Write(input.InputId);
            w.Write((byte)input.Trigger);
            w.Write((byte)input.Action);
            w.Write(input.Target);
            w.Write(input.Param);
            w.Write(input.DebounceMs);
            w.Write(input.Priority);
            w.Write(input.Reserved);
        }
    }

    private static void WriteSystemBloc(BinaryWriter w, AuroraSystemConfig sys)
    {
        WriteTlvHeader(w, AuroraBlocType.System, SystemPayloadSize);
        w.Write(sys.BootScene);
        w.Write(sys.TempDerating);
        w.Write(sys.HoursCounter);
        w.Write(sys.Reserved);
    }

    // ──────────────────────── Bloc Parsers ─────────────────────────────

    private static AuroraLedConfig ParseLedConfig(ReadOnlySpan<byte> data)
    {
        if (data.Length < LedPayloadSize)
            return new AuroraLedConfig();

        return new AuroraLedConfig
        {
            MaxPwm = data[0],
            SoftStartMs = BinaryPrimitives.ReadUInt16LittleEndian(data[1..]),
            Reserved = data[3],
        };
    }

    private static void ParseScenesBloc(ReadOnlySpan<byte> data, AuroraConfiguration config)
    {
        int pos = 0;
        int expectedSceneId = 0;

        // Lire les scènes (IDs séquentiels, 12 bytes chacune)
        while (pos + SceneSize <= data.Length && expectedSceneId < AuroraConfiguration.MaxScenes)
        {
            if (data[pos] != expectedSceneId)
                break;

            var scene = new AuroraScene
            {
                SceneId = data[pos],
                Flags = (AuroraSceneFlags)data[pos + 1],
                State = new AuroraLedState
                {
                    Mode = (AuroraLedMode)data[pos + 2],
                    Red = data[pos + 3],
                    Green = data[pos + 4],
                    Blue = data[pos + 5],
                    TOnMs = BinaryPrimitives.ReadUInt16LittleEndian(data[(pos + 6)..]),
                    TOffMs = BinaryPrimitives.ReadUInt16LittleEndian(data[(pos + 8)..]),
                    Repeat = data[pos + 10],
                    FadeTime = data[pos + 11],
                },
            };

            config.Scenes.Add(scene);
            pos += SceneSize;
            expectedSceneId++;
        }
    }

    private static void ParseInputsBloc(ReadOnlySpan<byte> data, AuroraConfiguration config)
    {
        int pos = 0;
        while (pos + InputPayloadSize <= data.Length && config.Inputs.Count < AuroraConfiguration.MaxInputs)
        {
            config.Inputs.Add(new AuroraInputConfig
            {
                InputId = data[pos],
                Trigger = (AuroraTrigger)data[pos + 1],
                Action = (AuroraAction)data[pos + 2],
                Target = data[pos + 3],
                Param = BinaryPrimitives.ReadUInt16LittleEndian(data[(pos + 4)..]),
                DebounceMs = BinaryPrimitives.ReadUInt16LittleEndian(data[(pos + 6)..]),
                Priority = data[pos + 8],
                Reserved = data[pos + 9],
            });
            pos += InputPayloadSize;
        }
    }

    private static AuroraSystemConfig ParseSystemConfig(ReadOnlySpan<byte> data)
    {
        if (data.Length < SystemPayloadSize)
            return new AuroraSystemConfig();

        return new AuroraSystemConfig
        {
            BootScene = data[0],
            TempDerating = data[1],
            HoursCounter = BinaryPrimitives.ReadUInt16LittleEndian(data[2..]),
            Reserved = BinaryPrimitives.ReadUInt32LittleEndian(data[4..]),
        };
    }

    // ──────────────────────── CRC-32 (IEEE 802.3) ─────────────────────

    private static readonly uint[] Crc32Table = BuildCrc32Table();

    private static uint[] BuildCrc32Table()
    {
        const uint polynomial = 0xEDB88320;
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int j = 0; j < 8; j++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ polynomial : crc >> 1;
            table[i] = crc;
        }
        return table;
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> data)
    {
        return ComputeCrc32(data, 0, data.Length);
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> data, int offset, int length)
    {
        uint crc = 0xFFFFFFFF;
        for (int i = offset; i < offset + length; i++)
            crc = (crc >> 8) ^ Crc32Table[(crc ^ data[i]) & 0xFF];
        return crc ^ 0xFFFFFFFF;
    }
}
