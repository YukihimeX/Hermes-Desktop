using System;
using HermesDesktop.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;

namespace HermesDesktop.Views;

public sealed partial class IntegrationsPage : Page
{
    private static readonly ResourceLoader ResourceLoader = new();
    private bool _suppressWhatsAppToggle;
    private bool _suppressWebhookToggle;

    public IntegrationsPage()
    {
        InitializeComponent();
        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        RefreshAll();
    }

    private void RefreshAll()
    {
        RefreshGatewayStatus();
        RefreshTelegramDisplay();
        RefreshDiscordDisplay();
        RefreshSlackDisplay();
        RefreshWhatsAppDisplay();
        RefreshMatrixDisplay();
        RefreshWebhookDisplay();
    }

    // =========================================================================
    // Gateway Status
    // =========================================================================

    private void RefreshGatewayStatus()
    {
        bool running = HermesEnvironment.IsGatewayRunning();
        bool installed = HermesEnvironment.HermesInstalled;

        GatewayStatusText.Text = running ? "Running" : "Stopped";
        GatewayIndicator.Fill = running
            ? (Brush)Application.Current.Resources["ConnectionOnlineBrush"]
            : (Brush)Application.Current.Resources["ConnectionOfflineBrush"];

        string state = HermesEnvironment.ReadGatewayState();
        GatewayStateText.Text = running
            ? $"State: {state}"
            : installed ? "Gateway is not running. Click Start to launch it." : "Hermes CLI not found. Install hermes first.";

        GatewayToggleButton.Content = running ? "Stop Gateway" : "Start Gateway";
        GatewayToggleButton.IsEnabled = installed;
    }

    private void GatewayToggle_Click(object sender, RoutedEventArgs e)
    {
        if (HermesEnvironment.IsGatewayRunning())
        {
            HermesEnvironment.StopGateway();
        }
        else
        {
            HermesEnvironment.StartGateway();
        }

        // Small delay then refresh
        DispatcherQueue.TryEnqueue(async () =>
        {
            await System.Threading.Tasks.Task.Delay(1500);
            RefreshGatewayStatus();
        });
    }

    private void GatewayRefresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshAll();
    }

    // =========================================================================
    // Telegram
    // =========================================================================

    private void RefreshTelegramDisplay()
    {
        var configToken = HermesEnvironment.ReadPlatformSetting("telegram", "token");
        var legacyToken = HermesEnvironment.ReadIntegrationSetting("telegram_bot_token");
        var envConfigured = HermesEnvironment.TelegramConfigured;
        var token = configToken ?? legacyToken;
        var hasToken = !string.IsNullOrWhiteSpace(token) || envConfigured;

        TelegramStatusText.Text = hasToken
            ? ResourceLoader.GetString("StatusConfigured")
            : ResourceLoader.GetString("StatusNotDetected");

        TelegramMaskedText.Text = !string.IsNullOrWhiteSpace(token)
            ? MaskToken(token)
            : envConfigured ? "Set via environment variable" : "Not configured";
    }

    private async void SaveTelegram_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var token = TelegramTokenBox.Password.Trim();
            if (string.IsNullOrEmpty(token))
            {
                SetStatus(TelegramSaveStatus, "Token cannot be empty.", false);
                return;
            }

            await HermesEnvironment.SavePlatformSettingAsync("telegram", "token", token);
            await HermesEnvironment.SavePlatformSettingAsync("telegram", "enabled", "true");
            SetStatus(TelegramSaveStatus, "Saved to config.yaml.", true);
            TelegramTokenBox.Password = "";
            RefreshTelegramDisplay();
        }
        catch (Exception ex)
        {
            SetStatus(TelegramSaveStatus, $"Error: {ex.Message}", false);
        }
    }

    // =========================================================================
    // Discord
    // =========================================================================

    private void RefreshDiscordDisplay()
    {
        var configToken = HermesEnvironment.ReadPlatformSetting("discord", "token");
        var legacyToken = HermesEnvironment.ReadIntegrationSetting("discord_bot_token");
        var envConfigured = HermesEnvironment.DiscordConfigured;
        var token = configToken ?? legacyToken;
        var hasToken = !string.IsNullOrWhiteSpace(token) || envConfigured;

        DiscordStatusText.Text = hasToken
            ? ResourceLoader.GetString("StatusConfigured")
            : ResourceLoader.GetString("StatusNotDetected");

        DiscordMaskedText.Text = !string.IsNullOrWhiteSpace(token)
            ? MaskToken(token)
            : envConfigured ? "Set via environment variable" : "Not configured";
    }

    private async void SaveDiscord_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var token = DiscordTokenBox.Password.Trim();
            if (string.IsNullOrEmpty(token))
            {
                SetStatus(DiscordSaveStatus, "Token cannot be empty.", false);
                return;
            }

            await HermesEnvironment.SavePlatformSettingAsync("discord", "token", token);
            await HermesEnvironment.SavePlatformSettingAsync("discord", "enabled", "true");
            SetStatus(DiscordSaveStatus, "Saved to config.yaml.", true);
            DiscordTokenBox.Password = "";
            RefreshDiscordDisplay();
        }
        catch (Exception ex)
        {
            SetStatus(DiscordSaveStatus, $"Error: {ex.Message}", false);
        }
    }

    // =========================================================================
    // Slack
    // =========================================================================

    private void RefreshSlackDisplay()
    {
        var configToken = HermesEnvironment.ReadPlatformSetting("slack", "token");
        var envConfigured = HermesEnvironment.SlackConfigured;
        var hasToken = !string.IsNullOrWhiteSpace(configToken) || envConfigured;

        SlackStatusText.Text = hasToken
            ? ResourceLoader.GetString("StatusConfigured")
            : ResourceLoader.GetString("StatusNotDetected");

        SlackMaskedText.Text = !string.IsNullOrWhiteSpace(configToken)
            ? MaskToken(configToken)
            : envConfigured ? "Set via environment variable" : "Not configured";
    }

    private async void SaveSlack_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var botToken = SlackBotTokenBox.Password.Trim();
            var appToken = SlackAppTokenBox.Password.Trim();

            if (string.IsNullOrEmpty(botToken))
            {
                SetStatus(SlackSaveStatus, "Bot token cannot be empty.", false);
                return;
            }

            await HermesEnvironment.SavePlatformSettingAsync("slack", "token", botToken);
            await HermesEnvironment.SavePlatformSettingAsync("slack", "enabled", "true");

            if (!string.IsNullOrEmpty(appToken))
            {
                await HermesEnvironment.SavePlatformSettingAsync("slack", "app_token", appToken);
            }

            SetStatus(SlackSaveStatus, "Saved to config.yaml.", true);
            SlackBotTokenBox.Password = "";
            SlackAppTokenBox.Password = "";
            RefreshSlackDisplay();
        }
        catch (Exception ex)
        {
            SetStatus(SlackSaveStatus, $"Error: {ex.Message}", false);
        }
    }

    // =========================================================================
    // WhatsApp
    // =========================================================================

    private void RefreshWhatsAppDisplay()
    {
        var envConfigured = HermesEnvironment.WhatsAppConfigured;

        WhatsAppStatusText.Text = envConfigured
            ? ResourceLoader.GetString("StatusConfigured")
            : ResourceLoader.GetString("StatusNotDetected");

        WhatsAppMaskedText.Text = envConfigured ? "Enabled" : "Not enabled";

        _suppressWhatsAppToggle = true;
        WhatsAppEnabledToggle.IsOn = envConfigured;
        _suppressWhatsAppToggle = false;
    }

    private async void WhatsAppToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressWhatsAppToggle) return;

        try
        {
            string value = WhatsAppEnabledToggle.IsOn ? "true" : "false";
            await HermesEnvironment.SavePlatformSettingAsync("whatsapp", "enabled", value);
            SetStatus(WhatsAppSaveStatus, "Saved to config.yaml.", true);
            RefreshWhatsAppDisplay();
        }
        catch (Exception ex)
        {
            SetStatus(WhatsAppSaveStatus, $"Error: {ex.Message}", false);
        }
    }

    // =========================================================================
    // Matrix
    // =========================================================================

    private void RefreshMatrixDisplay()
    {
        var configToken = HermesEnvironment.ReadPlatformSetting("matrix", "token");
        var envConfigured = HermesEnvironment.MatrixConfigured;
        var hasToken = !string.IsNullOrWhiteSpace(configToken) || envConfigured;

        MatrixStatusText.Text = hasToken
            ? ResourceLoader.GetString("StatusConfigured")
            : ResourceLoader.GetString("StatusNotDetected");

        MatrixMaskedText.Text = !string.IsNullOrWhiteSpace(configToken)
            ? MaskToken(configToken)
            : envConfigured ? "Set via environment variable" : "Not configured";

        // Load homeserver into text box if present in config
        var homeserver = HermesEnvironment.ReadPlatformSetting("matrix", "homeserver");
        if (!string.IsNullOrWhiteSpace(homeserver) && string.IsNullOrEmpty(MatrixHomeserverBox.Text))
        {
            MatrixHomeserverBox.Text = homeserver;
        }
    }

    private async void SaveMatrix_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var token = MatrixTokenBox.Password.Trim();
            var homeserver = MatrixHomeserverBox.Text.Trim();

            if (string.IsNullOrEmpty(token))
            {
                SetStatus(MatrixSaveStatus, "Access token cannot be empty.", false);
                return;
            }

            await HermesEnvironment.SavePlatformSettingAsync("matrix", "token", token);
            await HermesEnvironment.SavePlatformSettingAsync("matrix", "enabled", "true");

            if (!string.IsNullOrEmpty(homeserver))
            {
                await HermesEnvironment.SavePlatformSettingAsync("matrix", "homeserver", homeserver);
            }

            SetStatus(MatrixSaveStatus, "Saved to config.yaml.", true);
            MatrixTokenBox.Password = "";
            RefreshMatrixDisplay();
        }
        catch (Exception ex)
        {
            SetStatus(MatrixSaveStatus, $"Error: {ex.Message}", false);
        }
    }

    // =========================================================================
    // Webhook
    // =========================================================================

    private void RefreshWebhookDisplay()
    {
        var envConfigured = HermesEnvironment.WebhookConfigured;

        WebhookStatusText.Text = envConfigured
            ? ResourceLoader.GetString("StatusConfigured")
            : ResourceLoader.GetString("StatusNotDetected");

        WebhookMaskedText.Text = envConfigured ? "Enabled" : "Not enabled";

        _suppressWebhookToggle = true;
        WebhookEnabledToggle.IsOn = envConfigured;
        _suppressWebhookToggle = false;

        // Load port from config
        var port = HermesEnvironment.ReadPlatformSetting("webhook", "port");
        if (!string.IsNullOrWhiteSpace(port) && string.IsNullOrEmpty(WebhookPortBox.Text))
        {
            WebhookPortBox.Text = port;
        }
    }

    private async void WebhookToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressWebhookToggle) return;

        try
        {
            string value = WebhookEnabledToggle.IsOn ? "true" : "false";
            await HermesEnvironment.SavePlatformSettingAsync("webhook", "enabled", value);
            RefreshWebhookDisplay();
        }
        catch (Exception ex)
        {
            SetStatus(WebhookSaveStatus, $"Error: {ex.Message}", false);
        }
    }

    private async void SaveWebhook_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await HermesEnvironment.SavePlatformSettingAsync("webhook", "enabled", "true");

            var port = WebhookPortBox.Text.Trim();
            if (!string.IsNullOrEmpty(port))
            {
                await HermesEnvironment.SavePlatformSettingAsync("webhook", "port", port);
            }

            var secret = WebhookSecretBox.Password.Trim();
            if (!string.IsNullOrEmpty(secret))
            {
                await HermesEnvironment.SavePlatformSettingAsync("webhook", "secret", secret);
            }

            SetStatus(WebhookSaveStatus, "Saved to config.yaml.", true);
            WebhookSecretBox.Password = "";
            RefreshWebhookDisplay();
        }
        catch (Exception ex)
        {
            SetStatus(WebhookSaveStatus, $"Error: {ex.Message}", false);
        }
    }

    // =========================================================================
    // Shared helpers
    // =========================================================================

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        HermesEnvironment.OpenLogs();
    }

    private void OpenConfig_Click(object sender, RoutedEventArgs e)
    {
        HermesEnvironment.OpenConfig();
    }

    private static string MaskToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return "";
        if (token.Length <= 4) return "****";
        return "****" + token[^4..];
    }

    private void SetStatus(TextBlock statusBlock, string message, bool success)
    {
        statusBlock.Text = message;
        statusBlock.Foreground = success
            ? (Brush)Application.Current.Resources["ConnectionOnlineBrush"]
            : (Brush)Application.Current.Resources["ConnectionOfflineBrush"];
    }
}
