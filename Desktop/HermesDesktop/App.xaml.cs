using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Hermes.Agent.Core;
using Hermes.Agent.LLM;
using Hermes.Agent.Transcript;
using Hermes.Agent.Memory;
using Hermes.Agent.Skills;
using Hermes.Agent.Permissions;
using Hermes.Agent.Tasks;
using Hermes.Agent.Buddy;
using Hermes.Agent.Context;
using Hermes.Agent.Agents;
using Hermes.Agent.Coordinator;
using Hermes.Agent.Mcp;
using Hermes.Agent.Soul;
using Hermes.Agent.Tools;
using HermesDesktop.Services;
using System;
using System.IO;
using System.Net.Http;

namespace HermesDesktop;

public partial class App : Application
{
    /// <summary>Public access to the main window for theme switching from pages.</summary>
    public Window? MainWindow { get; private set; } // Updated from private _window

    /// <summary>Global service provider for DI — accessed by pages via App.Services.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Services = ConfigureServices();
        MainWindow = new MainWindow(); // Correctly assigning to the public property
        MainWindow.Activate();
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Logging
        services.AddLogging(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Information));

        // LLM config from environment/config.yaml
        var llmConfig = HermesEnvironment.CreateLlmConfig();
        services.AddSingleton(llmConfig);
        services.AddSingleton<HttpClient>();

        // Optional credential pool for multi-key rotation
        var credentialPool = HermesEnvironment.LoadCredentialPool();
        if (credentialPool is not null)
            services.AddSingleton(credentialPool);

        services.AddSingleton<IChatClient>(sp =>
        {
            var config = sp.GetRequiredService<LlmConfig>();
            var http = sp.GetRequiredService<HttpClient>();
            var pool = sp.GetService<CredentialPool>(); 
            return config.Provider?.ToLowerInvariant() switch
            {
                "anthropic" or "claude" => new AnthropicClient(config, http, pool),
                _ => new OpenAiClient(config, http, pool),
            };
        });

        // Hermes home directory logic...
        var hermesHome = HermesEnvironment.HermesHomePath;
        var projectDir = Path.Combine(hermesHome, "hermes-cs");
        Directory.CreateDirectory(projectDir);

        services.AddSingleton(sp => new TranscriptStore(Path.Combine(projectDir, "transcripts"), eagerFlush: true));
        services.AddSingleton(sp => new MemoryManager(Path.Combine(projectDir, "memory"), sp.GetRequiredService<IChatClient>(), sp.GetRequiredService<ILogger<MemoryManager>>()));
        services.AddSingleton(sp => new SkillManager(Path.Combine(projectDir, "skills"), sp.GetRequiredService<ILogger<SkillManager>>()));
        services.AddSingleton(sp => new PermissionManager(new PermissionContext(), sp.GetRequiredService<ILogger<PermissionManager>>()));
        services.AddSingleton(sp => new TaskManager(Path.Combine(projectDir, "tasks"), sp.GetRequiredService<ILogger<TaskManager>>()));
        services.AddSingleton(sp => new BuddyService(Path.Combine(projectDir, "buddy"), sp.GetRequiredService<IChatClient>()));
        services.AddSingleton(sp => new SoulService(hermesHome, sp.GetRequiredService<ILogger<SoulService>>()));
        services.AddSingleton(sp => new SoulExtractor(sp.GetRequiredService<IChatClient>(), sp.GetRequiredService<ILogger<SoulExtractor>>()));

        // Soul registry & Agent Profile Manager
        services.AddSingleton(sp => new SoulRegistry(new[] { Path.Combine(AppContext.BaseDirectory, "skills", "souls"), Path.Combine(projectDir, "souls") }, sp.GetRequiredService<ILogger<SoulRegistry>>()));
        services.AddSingleton(sp => new AgentProfileManager(Path.Combine(projectDir, "agents"), sp.GetRequiredService<SoulService>(), sp.GetRequiredService<ILogger<AgentProfileManager>>()));
        
        services.AddSingleton(sp => new TokenBudget(8000, 6));
        services.AddSingleton(sp => new PromptBuilder(SystemPrompts.Default));
        services.AddSingleton(sp => new ContextManager(sp.GetRequiredService<TranscriptStore>(), sp.GetRequiredService<IChatClient>(), sp.GetRequiredService<TokenBudget>(), sp.GetRequiredService<PromptBuilder>(), sp.GetRequiredService<ILogger<ContextManager>>(), sp.GetRequiredService<SoulService>()));
        services.AddSingleton(sp => new McpManager(sp.GetRequiredService<ILogger<McpManager>>()));
        services.AddSingleton<IToolRegistry, ToolRegistry>();

        services.AddSingleton(sp => new Agent(sp.GetRequiredService<IChatClient>(), sp.GetRequiredService<ILogger<Agent>>(), sp.GetRequiredService<PermissionManager>(), sp.GetRequiredService<TranscriptStore>(), sp.GetRequiredService<MemoryManager>(), sp.GetRequiredService<ContextManager>(), sp.GetRequiredService<SoulService>()));
        services.AddSingleton(sp => new AgentService(sp, sp.GetRequiredService<ILogger<AgentService>>(), sp.GetRequiredService<ILoggerFactory>(), sp.GetRequiredService<IChatClient>(), Path.Combine(projectDir, "worktrees")));
        services.AddSingleton(sp => new CoordinatorService(sp.GetRequiredService<AgentService>(), sp.GetRequiredService<TaskManager>(), sp.GetRequiredService<ILogger<CoordinatorService>>(), sp.GetRequiredService<IChatClient>(), Path.Combine(projectDir, "coordinator")));
        services.AddSingleton(sp => new Hermes.Agent.Skills.SkillInvoker(sp.GetRequiredService<SkillManager>(), sp.GetRequiredService<IChatClient>(), sp.GetRequiredService<ILogger<Hermes.Agent.Skills.SkillInvoker>>()));
        services.AddSingleton<HermesChatService>();

        var provider = services.BuildServiceProvider();
        RegisterAllTools(provider);
        InitializeMcpAsync(provider, projectDir);
        WirePermissionCallback(provider);

        return provider;
    }

    private static void WirePermissionCallback(IServiceProvider services)
    {
        var agent = services.GetRequiredService<Agent>();
        agent.PermissionPromptCallback = async (toolName, message) =>
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
            if (Application.Current is App app && app.MainWindow is not null) // Fix: Updated to check MainWindow
            {
                app.MainWindow.DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                        {
                            Title = $"Permission Required: {toolName}",
                            Content = message,
                            PrimaryButtonText = "Allow",
                            CloseButtonText = "Deny",
                            XamlRoot = app.MainWindow.Content.XamlRoot
                        };
                        var result = await dialog.ShowAsync();
                        tcs.TrySetResult(result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary);
                    }
                    catch { tcs.TrySetResult(false); }
                });
            }
            else { tcs.TrySetResult(false); }
            return await tcs.Task;
        };
    }

    // Helper registration methods (RegisterAllTools, InitializeMcpAsync, etc.) remain the same...
    private static void RegisterAllTools(IServiceProvider services) { /* ... */ }
    private static void RegisterAndTrack(Agent agent, IToolRegistry registry, ITool tool) { /* ... */ }
    private static async void InitializeMcpAsync(IServiceProvider services, string projectDir) { /* ... */ }
}
