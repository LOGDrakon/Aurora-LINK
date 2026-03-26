using System.Collections.Generic;
using System.Linq;

namespace Aurora_LINK.Configuration;

/// <summary>
/// Résultat de validation d'une configuration Aurora avant export .flora.
/// </summary>
public sealed class AuroraValidationResult
{
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];

    public bool IsValid => Errors.Count == 0;

    /// <summary>Résumé formaté pour affichage utilisateur.</summary>
    public string Summary
    {
        get
        {
            var parts = new List<string>();
            if (Errors.Count > 0)
                parts.Add($"Erreurs ({Errors.Count}) :\n" + string.Join("\n", Errors.Select(e => $"  ✗ {e}")));
            if (Warnings.Count > 0)
                parts.Add($"Avertissements ({Warnings.Count}) :\n" + string.Join("\n", Warnings.Select(w => $"  ⚠ {w}")));
            if (parts.Count == 0)
                parts.Add("Configuration valide.");
            return string.Join("\n\n", parts);
        }
    }
}

/// <summary>
/// Valide une <see cref="AuroraConfiguration"/> selon les règles DT-AURORA-MEM-001.
/// </summary>
public static class AuroraConfigValidator
{
    /// <summary>
    /// Effectue toutes les vérifications de conformité DT.
    /// </summary>
    public static AuroraValidationResult Validate(AuroraConfiguration config)
    {
        var result = new AuroraValidationResult();

        ValidateHeader(config, result);
        ValidateScenes(config, result);
        ValidateInputs(config, result);
        ValidateSize(config, result);

        return result;
    }

    private static void ValidateHeader(AuroraConfiguration config, AuroraValidationResult result)
    {
        if (config.Header.Magic != AuroraHeader.MagicValue)
            result.Errors.Add($"Magic invalide (0x{config.Header.Magic:X8}), attendu 0x{AuroraHeader.MagicValue:X8} (\"AURA\").");

        if (config.Header.Version != AuroraHeader.CurrentVersion)
            result.Warnings.Add($"Version du format ({config.Header.Version}) différente de la version courante ({AuroraHeader.CurrentVersion}).");
    }

    private static void ValidateScenes(AuroraConfiguration config, AuroraValidationResult result)
    {
        if (config.Scenes.Count > AuroraConfiguration.MaxScenes)
            result.Errors.Add($"Trop de scènes ({config.Scenes.Count}), maximum autorisé : {AuroraConfiguration.MaxScenes}.");

        // IDs séquentiels (DT §7.5 — scene_id : 0x00–0x0F, séquentiels)
        for (int i = 0; i < config.Scenes.Count; i++)
        {
            if (config.Scenes[i].SceneId != i)
                result.Errors.Add($"Scène à l'index {i} a l'ID {config.Scenes[i].SceneId}, attendu {i} (IDs séquentiels).");
        }

        // Au plus une scène auto-start (DT §7.6)
        int autoStartCount = config.Scenes.Count(s => s.Flags.HasFlag(AuroraSceneFlags.AutoStart));
        if (autoStartCount > 1)
            result.Errors.Add($"{autoStartCount} scènes ont le flag Auto-start, une seule est autorisée.");

        // Modes LED valides
        foreach (var scene in config.Scenes)
        {
            if (!System.Enum.IsDefined(scene.State.Mode))
                result.Errors.Add($"Scène {scene.SceneId} : mode LED invalide (0x{(byte)scene.State.Mode:X2}).");
        }

        if (config.Scenes.Count == 0)
            result.Warnings.Add("Aucune scène définie — le module n'aura aucun état lumineux disponible.");
    }

    private static void ValidateInputs(AuroraConfiguration config, AuroraValidationResult result)
    {
        if (config.Inputs.Count > AuroraConfiguration.MaxInputs)
            result.Errors.Add($"Trop d'entrées ({config.Inputs.Count}), maximum autorisé : {AuroraConfiguration.MaxInputs}.");

        var seenIds = new HashSet<byte>();
        foreach (var input in config.Inputs)
        {
            if (input.InputId >= AuroraConfiguration.MaxInputs)
                result.Errors.Add($"Entrée avec ID {input.InputId} hors plage (0–{AuroraConfiguration.MaxInputs - 1}).");

            if (!seenIds.Add(input.InputId))
                result.Errors.Add($"ID d'entrée {input.InputId} dupliqué.");

            if (!System.Enum.IsDefined(input.Trigger))
                result.Errors.Add($"Entrée {input.InputId} : trigger invalide (0x{(byte)input.Trigger:X2}).");

            if (!System.Enum.IsDefined(input.Action))
                result.Errors.Add($"Entrée {input.InputId} : action invalide (0x{(byte)input.Action:X2}).");

            // Si l'action est LoadScene, vérifier que la cible existe
            if (input.Action == AuroraAction.LoadScene && input.Target >= config.Scenes.Count)
                result.Warnings.Add($"Entrée I{input.InputId} : cible scène {input.Target} inexistante ({config.Scenes.Count} scène(s) définies).");
        }

        if (config.Inputs.Count == 0)
            result.Warnings.Add("Aucune entrée configurée — le module ne réagira à aucune entrée physique.");
    }

    private static void ValidateSize(AuroraConfiguration config, AuroraValidationResult result)
    {
        // Calculer la taille estimée des blocs TLV
        const int tlvHeader = 4;
        int ledBloc = tlvHeader + 4;
        int scenesBloc = tlvHeader + config.Scenes.Count * 12;
        int inputsBloc = tlvHeader + config.Inputs.Count * 10;
        int systemBloc = tlvHeader + 8;

        int totalTlv = ledBloc + scenesBloc + inputsBloc + systemBloc;
        int totalFile = AuroraHeader.Size + totalTlv
                      + AuroraConfiguration.CrcSize + AuroraHeader.SignatureSize;

        if (totalFile > AuroraConfiguration.FlashPageSize)
            result.Errors.Add($"Taille totale ({totalFile} bytes) dépasse la limite ({AuroraConfiguration.FlashPageSize} bytes).");

        int percent = (int)(100.0 * totalFile / AuroraConfiguration.FlashPageSize);
        if (percent > 90)
            result.Warnings.Add($"Utilisation mémoire élevée : {totalFile}/{AuroraConfiguration.FlashPageSize} bytes ({percent}%).");
    }
}
