using System.Collections.Generic;

namespace Aurora_LINK.Configuration;

/// <summary>
/// Configuration complète d'un module Aurora, représentant le contenu
/// d'une page Flash de 2048 bytes (DT-AURORA-MEM-001).
/// </summary>
public sealed class AuroraConfiguration
{
    public const int FlashPageSize = 2048;
    public const int CrcSize = 4;
    public const int MaxScenes = 16;
    public const int MaxInputs = 10;

    public AuroraHeader Header { get; set; } = new();
    public AuroraLedConfig LedConfig { get; set; } = new();
    public List<AuroraScene> Scenes { get; set; } = [];
    public List<AuroraInputConfig> Inputs { get; set; } = [];
    public AuroraSystemConfig SystemConfig { get; set; } = new();

    /// <summary>
    /// Crée une configuration par défaut correspondant au mode
    /// « configuration minimale » du firmware (DT-AURORA-MEM-001 §11.2).
    /// </summary>
    public static AuroraConfiguration CreateDefault()
    {
        return new AuroraConfiguration
        {
            Header = new AuroraHeader(),
            LedConfig = new AuroraLedConfig(),
            Scenes = [],
            Inputs = [],
            SystemConfig = new AuroraSystemConfig(),
        };
    }
}
