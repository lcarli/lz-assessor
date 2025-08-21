using LzAssessor.NewVersion.Models;
using LzAssessor.NewVersion.Persistence;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Web;

namespace LzAssessor.NewVersion.Triggers;

/// <summary>
/// HTTP triggers for attestation management
/// </summary>
public static class AttestationTriggers
{
    /// <summary>
    /// Submit a new attestation
    /// </summary>
    [Function(nameof(SubmitAttestation))]
    public static async Task<HttpResponseData> SubmitAttestation(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "attestation/submit")] HttpRequestData req,
        FunctionContext context)
    {
        var logger = context.GetLogger(nameof(SubmitAttestation));
        
        try
        {
            var requestBody = await req.ReadAsStringAsync();
            if (string.IsNullOrEmpty(requestBody))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteAsJsonAsync(new { error = "Request body is required" });
                return badRequestResponse;
            }

            var request = JsonSerializer.Deserialize<AttestationRequest>(requestBody, 
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            if (request == null)
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteAsJsonAsync(new { error = "Invalid request format" });
                return badRequestResponse;
            }

            // Validate required fields
            if (string.IsNullOrEmpty(request.TenantId) || 
                string.IsNullOrEmpty(request.CheckId) ||
                string.IsNullOrEmpty(request.AttestorName) ||
                string.IsNullOrEmpty(request.AttestorEmail))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteAsJsonAsync(new { error = "TenantId, CheckId, AttestorName, and AttestorEmail are required" });
                return badRequestResponse;
            }

            var attestationId = Guid.NewGuid().ToString();
            var now = DateTimeOffset.UtcNow;
            var expiresAt = now.AddDays(request.ExpirationDays);

            var attestation = new Attestation(
                Id: attestationId,
                TenantId: request.TenantId,
                CheckId: request.CheckId,
                AttestorName: request.AttestorName,
                AttestorEmail: request.AttestorEmail,
                Status: request.Status,
                Comments: request.Comments,
                SubmittedAt: now,
                ExpiresAt: expiresAt,
                Evidence: request.Evidence,
                Metadata: new Dictionary<string, object?>
                {
                    ["submissionMethod"] = "API",
                    ["userAgent"] = req.Headers.TryGetValues("User-Agent", out var userAgent) ? userAgent.FirstOrDefault() : null
                });

            var persistence = context.InstanceServices.GetRequiredService<IAttestationPersistence>();
            await persistence.SaveAttestationAsync(attestation);

            logger.LogInformation("Attestation {AttestationId} submitted for check {CheckId} by {AttestorName}", 
                attestationId, request.CheckId, request.AttestorName);

            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(new 
            { 
                attestationId,
                message = "Attestation submitted successfully",
                expiresAt = expiresAt.ToString("O")
            });

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to submit attestation");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }

    /// <summary>
    /// Get attestations for a tenant
    /// </summary>
    [Function(nameof(GetTenantAttestations))]
    public static async Task<HttpResponseData> GetTenantAttestations(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "attestation/tenant")] HttpRequestData req,
        FunctionContext context)
    {
        var logger = context.GetLogger(nameof(GetTenantAttestations));
        
        try
        {
            var query = HttpUtility.ParseQueryString(req.Url.Query);
            var tenantId = query["tenantId"];
            if (string.IsNullOrEmpty(tenantId))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteAsJsonAsync(new { error = "tenantId query parameter is required" });
                return badRequestResponse;
            }

            var persistence = context.InstanceServices.GetRequiredService<IAttestationPersistence>();
            var attestations = await persistence.GetTenantAttestationsAsync(tenantId);

            logger.LogInformation("Retrieved {AttestationCount} attestations for tenant {TenantId}", 
                attestations.Length, tenantId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new 
            { 
                tenantId,
                attestations,
                count = attestations.Length
            });

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get tenant attestations");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }

    /// <summary>
    /// Get attestations for a specific check
    /// </summary>
    [Function(nameof(GetCheckAttestations))]
    public static async Task<HttpResponseData> GetCheckAttestations(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "attestation/check")] HttpRequestData req,
        FunctionContext context)
    {
        var logger = context.GetLogger(nameof(GetCheckAttestations));
        
        try
        {
            var query = HttpUtility.ParseQueryString(req.Url.Query);
            var tenantId = query["tenantId"];
            var checkId = query["checkId"];

            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(checkId))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteAsJsonAsync(new { error = "tenantId and checkId parameters are required" });
                return badRequestResponse;
            }

            var persistence = context.InstanceServices.GetRequiredService<IAttestationPersistence>();
            var attestations = await persistence.GetAttestationsAsync(tenantId, checkId);

            logger.LogInformation("Retrieved {AttestationCount} attestations for check {CheckId} in tenant {TenantId}", 
                attestations.Length, checkId, tenantId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new 
            { 
                tenantId,
                checkId,
                attestations,
                count = attestations.Length
            });

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get check attestations");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new { error = ex.Message });
            return errorResponse;
        }
    }
}