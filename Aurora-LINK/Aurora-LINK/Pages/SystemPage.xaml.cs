using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Link.Client;
using Link.Client.Hashing;
using Link.Core.Frames;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Aurora_LINK.Pages
{
    public sealed partial class SystemPage : Page
    {
        private const string AppId = "AURORA";

        public SystemPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var device = App.MainWindow?.ConnectedDevice;
            if (device is null)
                return;

            DeviceModel.Text = device.DeviceInfo.Model ?? "Inconnu";
            DeviceFirmware.Text = !string.IsNullOrEmpty(device.DeviceInfo.Version)
                ? device.DeviceInfo.Version
                : "Inconnue";
            DeviceUid.Text = device.DeviceInfo.Uid ?? "Non disponible";
            DevicePort.Text = device.PortName;
        }

        private async void BtnChangePassword_Click(object sender, RoutedEventArgs e)
        {
            var client = App.MainWindow?.Client;
            var device = App.MainWindow?.ConnectedDevice;

            if (client is null || device is null)
            {
                await ShowErrorAsync("Aucun appareil connecté.");
                return;
            }

            var currentPassword = CurrentPasswordBox.Password;
            var newPassword = NewPasswordBox.Password;
            var confirmPassword = ConfirmPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                await ShowErrorAsync("Veuillez entrer le mot de passe actuel.");
                return;
            }

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                await ShowErrorAsync("Veuillez entrer un nouveau mot de passe.");
                return;
            }

            if (newPassword != confirmPassword)
            {
                await ShowErrorAsync("Les mots de passe ne correspondent pas.");
                return;
            }

            if (newPassword == currentPassword)
            {
                await ShowErrorAsync("Le nouveau mot de passe doit être différent de l'actuel.");
                return;
            }

            BtnChangePassword.IsEnabled = false;

            try
            {
                await ChangePasswordAsync(client, device.DeviceInfo.HashMethod,
                    currentPassword, newPassword);

                CurrentPasswordBox.Password = string.Empty;
                NewPasswordBox.Password = string.Empty;
                ConfirmPasswordBox.Password = string.Empty;

                await ShowSuccessAsync("Le mot de passe a été modifié avec succès.");
            }
            catch (TimeoutException)
            {
                await ShowErrorAsync("Délai d'attente dépassé. Réessayez.");
            }
            catch (InvalidOperationException ex)
            {
                await ShowErrorAsync(ex.Message);
            }
            finally
            {
                BtnChangePassword.IsEnabled = true;
            }
        }

        private static async Task ChangePasswordAsync(
            LinkClient client, string hashMethod,
            string currentPassword, string newPassword,
            CancellationToken ct = default)
        {
            var hashProvider = LinkHashProviderFactory.Create(hashMethod)
                ?? throw new NotSupportedException(
                    $"Algorithme de hash non supporté : {hashMethod}");

            // 1. AUTH_INIT — fresh nonce exchange
            string clientNonce = Convert.ToHexString(
                RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

            var initFrame = await client.SendCommandAsync(
                AppId, "AUTH_INIT", ct, clientNonce);

            string deviceNonce = initFrame.ReturnArguments.FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "Nonce du device manquant dans la réponse AUTH_INIT.");

            // 2. Hash the current password for verification
            //    HASH(nonces + HASH(currentPassword))
            string oldPasswordHash = hashProvider.ComputeHash(currentPassword);
            string hashedOld = hashProvider.ComputeHash(
                clientNonce + deviceNonce + oldPasswordHash);

            // 3. XOR-encrypt HASH(newPassword) with encryption key
            //    encryption_key = HASH(deviceNonce + clientNonce + HASH(currentPassword))
            //    (reversed nonce order so key differs from verification hash)
            string encKeyHex = hashProvider.ComputeHash(
                deviceNonce + clientNonce + oldPasswordHash);
            byte[] encKey = Convert.FromHexString(encKeyHex);
            string newPasswordHash = hashProvider.ComputeHash(newPassword);
            byte[] newHashBytes = Convert.FromHexString(newPasswordHash);
            byte[] encrypted = new byte[newHashBytes.Length];
            for (int i = 0; i < newHashBytes.Length; i++)
                encrypted[i] = (byte)(newHashBytes[i] ^ encKey[i % encKey.Length]);
            string encryptedHex = Convert.ToHexString(encrypted);

            // 4. Send CHPASSWD command
            var frame = await client.SendCommandAsync(
                AppId, "CHPASSWD", ct, hashedOld, encryptedHex);

            var args = frame.ReturnArguments;
            if (args.Count == 0 || args[0] != "OK")
            {
                string detail = args.Count > 1 ? args[1] : "ERREUR_INCONNUE";
                string message = detail switch
                {
                    "LOCKED" => "L'appareil est verrouillé. Reconnectez-vous.",
                    "INVALID_PASSWORD" => "Mot de passe actuel incorrect.",
                    "NO_AUTH_INIT" => "Erreur de session. Reconnectez-vous.",
                    "WEAK_PASSWORD" => "Le nouveau mot de passe est trop faible.",
                    _ => $"Échec du changement de mot de passe ({detail})."
                };
                throw new InvalidOperationException(message);
            }
        }

        private async Task ShowErrorAsync(string message)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Erreur",
                Content = message,
                CloseButtonText = "OK",
            };
            await dialog.ShowAsync();
        }

        private async Task ShowSuccessAsync(string message)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Succès",
                Content = message,
                CloseButtonText = "OK",
            };
            await dialog.ShowAsync();
        }
    }
}
