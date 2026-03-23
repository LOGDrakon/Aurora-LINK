using System;

namespace Aurora_LINK.Configuration;

/// <summary>
/// Fichier projet Aurora (.aurora) — contient les métadonnées et la configuration complète.
/// </summary>
public sealed class AuroraProject
{
    /// <summary>Version du format de projet.</summary>
    public int FormatVersion { get; set; } = 1;

    /// <summary>Nom du projet affiché dans l'interface.</summary>
    public string Name { get; set; } = "Nouveau projet";

    /// <summary>Description libre.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Date de création (UTC).</summary>
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Date de dernière modification (UTC).</summary>
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Configuration Aurora complète.</summary>
    public AuroraProjectConfig Config { get; set; } = new();
}

/// <summary>
/// Représentation JSON-friendly de la configuration Aurora.
/// Miroir de <see cref="AuroraConfiguration"/> mais sérialisable en JSON.
/// </summary>
public sealed class AuroraProjectConfig
{
    public AuroraLedConfigDto Led { get; set; } = new();
    public AuroraSceneDto[] Scenes { get; set; } = [];
    public AuroraInputDto[] Inputs { get; set; } = [];
    public AuroraSystemConfigDto System { get; set; } = new();
}

public sealed class AuroraLedConfigDto
{
    public byte MaxPwm { get; set; } = 0xFF;
    public ushort SoftStartMs { get; set; }
}

public sealed class AuroraSceneDto
{
    public byte SceneId { get; set; }
    public byte Flags { get; set; }
    public byte Mode { get; set; }
    public byte Red { get; set; }
    public byte Green { get; set; }
    public byte Blue { get; set; }
    public ushort TOnMs { get; set; }
    public ushort TOffMs { get; set; }
    public byte Repeat { get; set; }
    public byte FadeTime { get; set; }
}

public sealed class AuroraInputDto
{
    public byte InputId { get; set; }
    public byte Trigger { get; set; }
    public byte Action { get; set; }
    public byte Target { get; set; }
    public ushort Param { get; set; }
    public ushort DebounceMs { get; set; } = 20;
    public byte Priority { get; set; }
}

public sealed class AuroraSystemConfigDto
{
    public byte BootScene { get; set; } = 0xFF;
    public byte TempDerating { get; set; }
    public ushort HoursCounter { get; set; }
}

/// <summary>
/// Conversions entre <see cref="AuroraConfiguration"/> et <see cref="AuroraProjectConfig"/>.
/// </summary>
public static class AuroraProjectMapper
{
    public static AuroraProjectConfig ToProjectConfig(AuroraConfiguration config)
    {
        var dto = new AuroraProjectConfig
        {
            Led = new AuroraLedConfigDto
            {
                MaxPwm = config.LedConfig.MaxPwm,
                SoftStartMs = config.LedConfig.SoftStartMs,
            },
            System = new AuroraSystemConfigDto
            {
                BootScene = config.SystemConfig.BootScene,
                TempDerating = config.SystemConfig.TempDerating,
                HoursCounter = config.SystemConfig.HoursCounter,
            },
        };

        var scenes = new AuroraSceneDto[config.Scenes.Count];
        for (int i = 0; i < config.Scenes.Count; i++)
        {
            var s = config.Scenes[i];
            scenes[i] = new AuroraSceneDto
            {
                SceneId = s.SceneId,
                Flags = (byte)s.Flags,
                Mode = (byte)s.State.Mode,
                Red = s.State.Red,
                Green = s.State.Green,
                Blue = s.State.Blue,
                TOnMs = s.State.TOnMs,
                TOffMs = s.State.TOffMs,
                Repeat = s.State.Repeat,
                FadeTime = s.State.FadeTime,
            };
        }
        dto.Scenes = scenes;

        var inputs = new AuroraInputDto[config.Inputs.Count];
        for (int i = 0; i < config.Inputs.Count; i++)
        {
            var inp = config.Inputs[i];
            inputs[i] = new AuroraInputDto
            {
                InputId = inp.InputId,
                Trigger = (byte)inp.Trigger,
                Action = (byte)inp.Action,
                Target = inp.Target,
                Param = inp.Param,
                DebounceMs = inp.DebounceMs,
                Priority = inp.Priority,
            };
        }
        dto.Inputs = inputs;

        return dto;
    }

    public static AuroraConfiguration ToConfiguration(AuroraProjectConfig dto)
    {
        var config = new AuroraConfiguration
        {
            LedConfig = new AuroraLedConfig
            {
                MaxPwm = dto.Led.MaxPwm,
                SoftStartMs = dto.Led.SoftStartMs,
            },
            SystemConfig = new AuroraSystemConfig
            {
                BootScene = dto.System.BootScene,
                TempDerating = dto.System.TempDerating,
                HoursCounter = dto.System.HoursCounter,
            },
        };

        foreach (var s in dto.Scenes)
        {
            config.Scenes.Add(new AuroraScene
            {
                SceneId = s.SceneId,
                Flags = (AuroraSceneFlags)s.Flags,
                State = new AuroraLedState
                {
                    Mode = (AuroraLedMode)s.Mode,
                    Red = s.Red,
                    Green = s.Green,
                    Blue = s.Blue,
                    TOnMs = s.TOnMs,
                    TOffMs = s.TOffMs,
                    Repeat = s.Repeat,
                    FadeTime = s.FadeTime,
                },
            });
        }

        foreach (var inp in dto.Inputs)
        {
            config.Inputs.Add(new AuroraInputConfig
            {
                InputId = inp.InputId,
                Trigger = (AuroraTrigger)inp.Trigger,
                Action = (AuroraAction)inp.Action,
                Target = inp.Target,
                Param = inp.Param,
                DebounceMs = inp.DebounceMs,
                Priority = inp.Priority,
            });
        }

        return config;
    }
}
