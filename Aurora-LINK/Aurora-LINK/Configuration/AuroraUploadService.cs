using System;
using System.Threading;
using System.Threading.Tasks;
using Link.Client;
using Link.Core.Frames;

namespace Aurora_LINK.Configuration;

/// <summary>
/// Téléversement d'un programme .flora vers un module Aurora via le protocole LINK.
///
/// Protocole de transfert :
///   1. UPLOAD:START:&lt;taille&gt;     → device répond OK (prêt à recevoir)
///   2. UPLOAD:DATA:&lt;seq&gt;:&lt;hex&gt;  → device répond OK (paquet reçu)
///   3. UPLOAD:END                 → device vérifie l'intégrité, répond OK ou ERR
///
/// Le SDK LINK gère le fractionnement des trames &gt;64 bytes au niveau transport.
/// </summary>
public static class AuroraUploadService
{
    private const string AppId = "AURORA";
    private const string Command = "UPLOAD";

    // Le SDK gère le fractionnement des trames au niveau transport.
    // 128 octets bruts par paquet = bon compromis vitesse/fiabilité.
    private const int MaxChunkSize = 128;

    /// <summary>
    /// Envoie un programme .flora vers le device connecté.
    /// </summary>
    /// <param name="client">Client LINK connecté.</param>
    /// <param name="data">Données binaires .flora à transmettre.</param>
    /// <param name="progress">Progression (bytes envoyés, total).</param>
    /// <param name="ct">Jeton d'annulation.</param>
    public static async Task UploadAsync(
        LinkClient client,
        byte[] data,
        IProgress<(int Sent, int Total)>? progress = null,
        CancellationToken ct = default)
    {
        // 1. START — notifier le device de la taille du programme
        var startFrame = await client.SendCommandAsync(
            AppId, Command, ct, "START", data.Length.ToString());

        ValidateResponse(startFrame, "Le device a refusé le téléversement");

        // 2. DATA — envoyer les paquets séquentiels
        int seq = 0;
        for (int offset = 0; offset < data.Length; offset += MaxChunkSize)
        {
            ct.ThrowIfCancellationRequested();

            int len = Math.Min(MaxChunkSize, data.Length - offset);
            string hex = Convert.ToHexString(data, offset, len);

            var dataFrame = await client.SendCommandAsync(
                AppId, Command, ct, "DATA", seq.ToString(), hex);

            ValidateResponse(dataFrame, $"Erreur lors de l'envoi du paquet {seq}");

            seq++;
            progress?.Report((offset + len, data.Length));
        }

        // 3. END — le device vérifie l'intégrité du programme reçu
        var endFrame = await client.SendCommandAsync(AppId, Command, ct, "END");

        ValidateResponse(endFrame, "Vérification d'intégrité échouée sur le device");
    }

    private static void ValidateResponse(LinkFrame frame, string errorContext)
    {
        var args = frame.ReturnArguments;

        if (args.Count == 0 || args[0] != "OK")
        {
            string detail = args.Count > 0 ? string.Join(":", args) : "pas de réponse";
            throw new InvalidOperationException($"{errorContext} ({detail}).");
        }
    }
}
