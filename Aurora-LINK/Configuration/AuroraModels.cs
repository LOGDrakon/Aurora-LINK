namespace Aurora_LINK.Configuration;

/// <summary>
/// En-tête de page Flash (DT-AURORA-MEM-001 §4). 16 bytes fixes.
/// </summary>
public sealed class AuroraHeader
{
    public const uint MagicValue = 0x41555241; // ASCII "AURA"
    public const uint SignatureValue = 0x464C4F52; // ASCII "FLOR" — fin de fichier .flora
    public const byte CurrentVersion = 0x02;
    public const int Size = 16;
    public const int SignatureSize = 4;

    public uint Magic { get; set; } = MagicValue;
    public byte Version { get; set; } = CurrentVersion;
    public byte NumBlocs { get; set; }
    public ushort TotalLength { get; set; }
    public uint WriteCount { get; set; }
    public uint Reserved { get; set; } = 0xFFFFFFFF;
}

/// <summary>
/// Configuration matérielle du canal LED unique (DT-AURORA-MEM-001 §6). 4 bytes.
/// Les deux LEDs physiques sont pilotées de manière synchrone.
/// </summary>
public sealed class AuroraLedConfig
{
    public byte MaxPwm { get; set; } = 0xFF;
    public ushort SoftStartMs { get; set; }
    public byte Reserved { get; set; } = 0xFF;
}

/// <summary>
/// État du canal lumineux dans une scène (DT-AURORA-MEM-001 §7.2). 10 bytes.
/// Couleur RGB appliquée aux deux LEDs synchrones.
/// </summary>
public sealed class AuroraLedState
{
    public AuroraLedMode Mode { get; set; } = AuroraLedMode.Off;
    public byte Red { get; set; }
    public byte Green { get; set; }
    public byte Blue { get; set; }
    public ushort TOnMs { get; set; }
    public ushort TOffMs { get; set; }
    public byte Repeat { get; set; }
    public byte FadeTime { get; set; }
}

/// <summary>
/// Scène d'éclairage (DT-AURORA-MEM-001 §7.5). 12 bytes.
/// </summary>
public sealed class AuroraScene
{
    public byte SceneId { get; set; }
    public AuroraSceneFlags Flags { get; set; }
    public AuroraLedState State { get; set; } = new();
}

/// <summary>
/// Règle d'entrée logique (DT-AURORA-MEM-001 §8.2). 10 bytes.
/// </summary>
public sealed class AuroraInputConfig
{
    public byte InputId { get; set; }
    public AuroraTrigger Trigger { get; set; }
    public AuroraAction Action { get; set; }
    public byte Target { get; set; }
    public ushort Param { get; set; }
    public ushort DebounceMs { get; set; } = 20;
    public byte Priority { get; set; }
    public byte Reserved { get; set; } = 0xFF;
}

/// <summary>
/// Paramètres système du module (DT-AURORA-MEM-001 §9). 8 bytes.
/// </summary>
public sealed class AuroraSystemConfig
{
    public byte BootScene { get; set; } = 0xFF;
    public byte TempDerating { get; set; }
    public ushort HoursCounter { get; set; }
    public uint Reserved { get; set; } = 0xFFFFFFFF;
}
