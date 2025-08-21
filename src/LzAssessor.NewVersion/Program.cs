using Azure.Identity;
using Azure.Monitor.Query;
using LzAssessor.NewVersion.Engine;
using LzAssessor.NewVersion.Executors;
using LzAssessor.NewVersion.Persistence;
using LzAssessor.NewVersion.Specs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureAppConfiguration(config =>
    {
        config.AddJsonFile("appsettings.json", optional: true)
              .AddJsonFile("local.settings.json", optional: true)
              .AddEnvironmentVariables();
    })
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        // HTTP Client for external API calls
        services.AddHttpClient();

        // Azure clients
        services.AddSingleton(provider =>
        {
            var credential = new DefaultAzureCredential();
            return new LogsQueryClient(credential);
        });

        // Core services
        services.AddSingleton<IDiscoveryService, AzureDiscoveryService>();
        services.AddSingleton<ExecutionRouter>();
        services.AddSingleton<IAttestationEvaluationService, AttestationEvaluationService>();

        // Spec loading
        services.AddSingleton<ISpecLoader, HttpSpecLoader>();

        // Executors
        services.AddSingleton<IExecutor, GraphExecutor>();
        services.AddSingleton<IExecutor, ArmExecutor>();
        services.AddSingleton<IExecutor, CostExecutor>();

        // Persistence - use in-memory for development, Log Analytics for production
        var useLogAnalytics = !string.IsNullOrEmpty(configuration["LogAnalytics:WorkspaceId"]);
        if (useLogAnalytics)
        {
            services.AddSingleton<IAssessmentPersistence, LogAnalyticsPersistence>();
        }
        else
        {
            services.AddSingleton<IAssessmentPersistence, InMemoryPersistence>();
        }

        // Attestation persistence - for now, always use in-memory
        // TODO: Implement Log Analytics-based attestation persistence
        services.AddSingleton<IAttestationPersistence, InMemoryAttestationPersistence>();
    })
    .Build();

await host.RunAsync();