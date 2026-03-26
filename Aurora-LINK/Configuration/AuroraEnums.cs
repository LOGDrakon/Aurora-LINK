using System;

namespace Aurora_LINK.Configuration;

/// <summary>
/// Identifiant de type pour les blocs TLV (DT-AURORA-MEM-001 §5.2).
/// </summary>
public enum AuroraBlocType : byte
{
    Leds    = 0x01,
    Scenes  = 0x02,
    Inputs  = 0x03,
    System  = 0x04,
}

/// <summary>
/// Mode d'animation du canal LED (DT-AURORA-MEM-001 §7.3).
/// </summary>
public enum AuroraLedMode : byte
{
    Off     = 0x00,
    Static  = 0x01,
    Blink   = 0x02,
    Fade    = 0x03,
    Burst   = 0x04,
    Double  = 0x05,
}

/// <summary>
/// Type de déclencheur pour une entrée logique (DT-AURORA-MEM-001 §8.3).
/// </summary>
public enum AuroraTrigger : byte
{
    Rising     = 0x00,
    Falling    = 0x01,
    High       = 0x02,
    Low        = 0x03,
    DoubleTap  = 0x04,
    LongPress  = 0x05,
    Pulse      = 0x06,
}

/// <summary>
/// Action à exécuter lors du déclenchement d'une entrée (DT-AURORA-MEM-001 §8.4).
/// </summary>
public enum AuroraAction : byte
{
    LoadScene = 0x00,
    SetBright = 0x01,
    Toggle    = 0x02,
    AllOff    = 0x03,
    DimUp     = 0x04,
    DimDown   = 0x05,
    Lock      = 0x06,
    Unlock    = 0x07,
    Identify  = 0x08,
}

/// <summary>
/// Drapeaux de scène (DT-AURORA-MEM-001 §7.6).
/// </summary>
[Flags]
public enum AuroraSceneFlags : byte
{
    None      = 0x00,
    AutoStart = 0x01,
}
