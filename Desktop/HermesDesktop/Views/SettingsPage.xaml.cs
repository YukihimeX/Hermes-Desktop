using System;
using System.Linq;
using Hermes.Agent.LLM;
using HermesDesktop.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;

namespace HermesDesktop.Views;

public sealed partial class SettingsPage : Page
{
    private static readonly ResourceLoader ResourceLoader = new();

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnPageLoaded;
    }

    public string HermesHomePath => HermesEnvironment.DisplayHermesHomePath;
    public string HermesConfigPath => HermesEnvironment.DisplayHermesConfigPath;
    public string HermesLogsPath => HermesEnvironment.DisplayHermesLogsPath;
    public string HermesWorkspacePath => HermesEnvironment.DisplayHermesWorkspacePath;
    public string TelegramStatus => HermesEnvironment.TelegramConfigured ? "Detected" : "Not Detected";
    public string DiscordStatus => HermesEnvironment.DiscordConfigured ? "Detected" : "Not Detected";

    private bool _suppressModelComboEvent;

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        var provider = HermesEnvironment.ModelProvider.ToLowerInvariant();
        PopulateProviderCombo(provider);
        BaseUrlBox.Text = HermesEnvironment.ModelBaseUrl;
        ModelBox.Text = HermesEnvironment.DefaultModel;
        ApiKeyBox.Password = HermesEnvironment.ModelApiKey ?? "";
        PopulateModelCombo(provider);
        SelectCurrentModel(HermesEnvironment.DefaultModel);
    }

    // --- APPEARANCE HANDLERS ---
    private void BrandCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateTheme();
    }

    private void ThemeModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateTheme();
    }

    private void UpdateTheme()
    {
        if (BrandCombo?.SelectedItem is ComboBoxItem brandItem && 
            ThemeModeCombo?.SelectedItem is ComboBoxItem modeItem)
        {
            string brand = brandItem.Tag?.ToString() ?? "gemini";
            if (Enum.TryParse(modeItem.Tag?.ToString(), out ElementTheme theme))
            {
                if (Application.Current is App app && app.MainWindow is MainWindow mainWin)
                {
                    mainWin.ApplyBrandTheme(brand, theme);
                }
            }
        }
    }

    // --- RESTORED MODEL CONFIG HELPERS ---
    private void PopulateProviderCombo(string currentProvider)
    {
        for (int i = 0; i < ProviderCombo.Items.Count; i++)
        {
            if (ProviderCombo.Items[i] is ComboBoxItem item && item.Tag?.ToString() == currentProvider)
            {
                ProviderCombo.SelectedIndex = i;
                break;
            }
        }
    }

    private void PopulateModelCombo(string provider)
    {
        _suppressModelComboEvent = true;
        ModelCombo.Items.Clear();
        if (provider == "openai") { ModelCombo.Items.Add("gpt-4o"); ModelCombo.Items.Add("gpt-4-turbo"); }
        else if (provider == "anthropic") { ModelCombo.Items.Add("claude-3-5-sonnet-20240620"); }
        _suppressModelComboEvent = false;
    }

    private void SelectCurrentModel(string model)
    {
        for (int i = 0; i < ModelCombo.Items.Count; i++)
        {
            if (ModelCombo.Items[i].ToString() == model)
            {
                ModelCombo.SelectedIndex = i;
                return;
            }
        }
        ModelCombo.SelectedIndex = -1;
    }

    private void ProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProviderCombo.SelectedItem is ComboBoxItem item)
        {
            PopulateModelCombo(item.Tag?.ToString() ?? "");
        }
    }

    private void ModelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_suppressModelComboEvent && ModelCombo.SelectedItem != null)
        {
            ModelBox.Text = ModelCombo.SelectedItem.ToString();
        }
    }

    private async void SaveModelConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var providerTag = (ProviderCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "local";
            await HermesEnvironment.SaveModelConfigAsync(providerTag, BaseUrlBox.Text, ModelBox.Text, ApiKeyBox.Password);
            ModelSaveStatus.Text = "Saved successfully. Restart required.";
        }
        catch (Exception ex) { ModelSaveStatus.Text = $"Error: {ex.Message}"; }
    }
}
