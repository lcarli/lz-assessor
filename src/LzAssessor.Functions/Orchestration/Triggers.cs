using System.Net;
using System.Text.Json;
using LzAssessor.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;

namespace LzAssessor.Functions.Orchestration;

public class Triggers
{
    // HTTP: start now
    [Function(nameof(StartAssessmentHttp))]
    public async Task<HttpResponseData> StartAssessmentHttp(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client)
    {
        var body = await JsonSerializer.DeserializeAsync<AssessmentRequest>(req.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web)) 
                   ?? new AssessmentRequest();

        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(AssessmentOrchestrator.RunAssessmentOrchestrator), body);
        var resp = req.CreateResponse(HttpStatusCode.Accepted);
        await resp.WriteStringAsync($"Started orchestration: {instanceId}");
        return resp;
    }

    // TIMER: agenda diária (06:00 UTC)
    [Function(nameof(ScheduleTimer))]
    public async Task ScheduleTimer([TimerTrigger("0 0 6 * * *")] TimerInfo _,
        [DurableClient] DurableTaskClient client)
    {
        var req = new AssessmentRequest(); // usa defaults do appsettings
        await client.ScheduleNewOrchestrationInstanceAsync(nameof(AssessmentOrchestrator.RunAssessmentOrchestrator), req);
    }
}