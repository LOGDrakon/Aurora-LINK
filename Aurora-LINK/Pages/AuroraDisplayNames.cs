using Aurora_LINK.Configuration;

namespace Aurora_LINK.Pages;

/// <summary>
/// Labels francophones pour les valeurs des énumérations Aurora.
/// </summary>
public static class AuroraDisplayNames
{
    public static string GetTriggerName(AuroraTrigger trigger) => trigger switch
    {
        AuroraTrigger.Rising    => "Front montant",
        AuroraTrigger.Falling   => "Front descendant",
        AuroraTrigger.High      => "Niveau haut",
        AuroraTrigger.Low       => "Niveau bas",
        AuroraTrigger.DoubleTap => "Double impulsion",
        AuroraTrigger.LongPress => "Appui long",
        AuroraTrigger.Pulse     => "Impulsion calibrée",
        _ => trigger.ToString(),
    };

    public static string GetActionName(AuroraAction action) => action switch
    {
        AuroraAction.LoadScene => "Charger scène",
        AuroraAction.SetBright => "Fixer luminosité",
        AuroraAction.Toggle    => "Basculer ON/OFF",
        AuroraAction.AllOff    => "Tout éteindre",
        AuroraAction.DimUp     => "Augmenter luminosité",
        AuroraAction.DimDown   => "Diminuer luminosité",
        AuroraAction.Lock      => "Verrouiller entrées",
        AuroraAction.Unlock    => "Déverrouiller entrées",
        AuroraAction.Identify  => "Identification visuelle",
        _ => action.ToString(),
    };

    public static string GetLedModeName(AuroraLedMode mode) => mode switch
    {
        AuroraLedMode.Off     => "Éteint",
        AuroraLedMode.Static  => "Fixe",
        AuroraLedMode.Blink   => "Clignotement",
        AuroraLedMode.Fade    => "Respiration",
        AuroraLedMode.Burst   => "Burst",
        AuroraLedMode.Double  => "Double flash",
        _ => mode.ToString(),
    };
}
