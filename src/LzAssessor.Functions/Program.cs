using LzAssessor.Functions.Engine;
using LzAssessor.Functions.Specs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureAppConfiguration(cfg =>
    {
        cfg.AddJsonFile("appsettings.json", optional: true)
           .AddEnvironmentVariables();
    })
    .ConfigureFunctionsWorkerDefaults(builder =>
    {
        builder.AddDurableTask();
    })
    .ConfigureServices((ctx, services) =>
    {
        // HttpClient p/ baixar specs
        services.AddHttpClient();

        // Providers/Engine
        services.AddSingleton<IChecklistProvider, HttpChecklistProvider>();
        services.AddSingleton<ExecutionRouter>();

        // Executors (stubs prontos; implementaremos aos poucos)
        services.AddSingleton<IExecutor, ArmExecutor>();
        services.AddSingleton<IExecutor, GraphExecutor>();
        services.AddSingleton<IExecutor, CostExecutor>();
    })
    .Build();

host.Run();