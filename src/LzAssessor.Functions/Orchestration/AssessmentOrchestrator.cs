using LzAssessor.Functions.Engine;
using LzAssessor.Functions.Models;
using LzAssessor.Functions.Specs;
using Microsoft.DurableTask;
using Microsoft.Azure.Functions.Worker;

namespace LzAssessor.Functions.Orchestration;

public class AssessmentOrchestrator
{
    // ORCHESTRATOR
    [Function(nameof(RunAssessmentOrchestrator))]
    public async Task<List<AssessmentResult>> RunAssessmentOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext ctx)
    {
        var input = ctx.GetInput<AssessmentRequest>() ?? new AssessmentRequest();
        var runId = ctx.CurrentUtcDateTime.ToString("o");

        // 1) discovery (snapshot)
        var snapshot = await ctx.CallActivityAsync<DiscoverySnapshot>(nameof(Activities.DiscoverActivity), null);

        // 2) carregar spec (por activity p/ permitir I/O)
        var spec = await ctx.CallActivityAsync<SpecFile>(nameof(Activities.LoadSpecActivity), input);

        // filtra categoria
        var category = input.Category ?? spec.Category;
        var checks = spec.Checks.Where(c => spec.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();

        // 3) fan-out: executar cada check
        var tasks = new List<Task<AssessmentResult>>();
        foreach (var check in checks)
        {
            var payload = new Activities.ExecuteCheckPayload(runId, snapshot, check, input.Scope);
            tasks.Add(ctx.CallActivityAsync<AssessmentResult>(nameof(Activities.ExecuteCheckActivity), payload));
        }

        var results = await Task.WhenAll(tasks);

        // 4) persistir (sink) – por enquanto só retorna; depois plugamos Log Analytics
        // await ctx.CallActivityAsync(nameof(Activities.PersistResultsActivity), results);

        return results.ToList();
    }
}