using System;
using System.Collections.Generic;
using HermesDesktop.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Graphics;

namespace HermesDesktop;

public sealed partial class MainWindow : Window
{
    private static readonly IReadOnlyDictionary<string, System.Type> PageMap = new Dictionary<string, System.Type>
    {
        ["dashboard"] = typeof(DashboardPage),
        ["chat"] = typeof(ChatPage),
        ["agent"] = typeof(AgentPage),
        ["skills"] = typeof(SkillsPage),
        ["memory"] = typeof(MemoryPage),
        ["buddy"] = typeof(BuddyPage),
        ["integrations"] = typeof(IntegrationsPage),
        ["settings"] = typeof(SettingsPage),
    };

    private static readonly ResourceLoader ResourceLoader = new();

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        Title = ResourceLoader.GetString("WindowTitle");
        AppTitleBar.Title = Title;
        AppWindow.Resize(new SizeInt32(1480, 960));
        AppWindow.SetIcon("Assets/AppIcon.ico");

        ShellNavigation.SelectedItem = ChatNavItem;
        NavigateToTag("chat");
    }

    public void ApplyBrandTheme(string brand, ElementTheme mode)
    {
        if (this.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = mode; // Instantly swaps Light/Dark
            
            var dictionaries = Application.Current.Resources.ThemeDictionaries;
            var activeThemeKey = mode == ElementTheme.Dark ? "Dark" : "Light";
            
            if (dictionaries.TryGetValue(activeThemeKey, out object? themeDictObj) && themeDictObj is ResourceDictionary themeDict)
            {
                switch (brand.ToLower())
                {
                    case "ollama":
                        themeDict["AppAccentBrush"] = Microsoft.UI.Colors.Cyan;
                        themeDict["AppWindowBackgroundBrush"] = mode == ElementTheme.Dark ? 
                            UIHelper.GetColorFromHex("#000000") : UIHelper.GetColorFromHex("#FFFFFF"); 
                        break;
                    case "claude":
                        themeDict["AppAccentBrush"] = UIHelper.GetColorFromHex("#D97757");
                        themeDict["AppWindowBackgroundBrush"] = mode == ElementTheme.Dark ? 
                            UIHelper.GetColorFromHex("#151515") : UIHelper.GetColorFromHex("#F5F2ED");
                        break;
                    case "gemini":
                    default:
                        themeDict["AppAccentBrush"] = UIHelper.GetColorFromHex("#1A73E8");
                        themeDict["AppWindowBackgroundBrush"] = mode == ElementTheme.Dark ? 
                            UIHelper.GetColorFromHex("#0B0B0B") : UIHelper.GetColorFromHex("#F8F9FA");
                        break;
                }
            }
        }
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is string tag) NavigateToTag(tag);
    }

    private void NavigateToTag(string tag)
    {
        if (PageMap.TryGetValue(tag, out System.Type? pageType) && ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}

public static class UIHelper 
{
    public static Windows.UI.Color GetColorFromHex(string hex)
    {
        hex = hex.Replace("#", "");
        byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
        byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
        byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
        return Windows.UI.Color.FromArgb(255, r, g, b);
    }
}
