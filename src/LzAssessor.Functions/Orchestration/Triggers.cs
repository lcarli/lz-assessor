using System.Net;
using System.Text.Json;
using LzAssessor.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace LzAssessor.Functions.Orchestration;

public class Triggers
{
    // HTTP Health check simple
    [Function(nameof(Health))]
    public async Task<HttpResponseData> Health([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
    {
        var resp = req.CreateResponse(HttpStatusCode.OK);
        await resp.WriteStringAsync("LZ Assessor Functions is running");
        return resp;
    }
    
    // HTTP Start assessment (sans Durable pour l'instant)
    [Function(nameof(StartAssessmentHttp))]
    public async Task<HttpResponseData> StartAssessmentHttp([HttpTrigger(AuthorizationLevel.Function, "post", Route = "assessment/start")] HttpRequestData req)
    {
        var resp = req.CreateResponse(HttpStatusCode.Accepted);
        await resp.WriteStringAsync("Assessment start accepted (simplified without orchestration)");
        return resp;
    }
}