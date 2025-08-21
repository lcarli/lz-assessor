using Azure.Core;
using Azure.Identity;
using LzAssessor.NewVersion.Engine;
using LzAssessor.NewVersion.Models;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Text.Json;

namespace LzAssessor.NewVersion.Executors;

/// <summary>
/// Executor for Microsoft Graph API checks (Entra ID)
/// </summary>
public sealed class GraphExecutor : ExecutorBase
{
    private readonly GraphServiceClient _graphClient;

    public override string Name => "graph";

    public GraphExecutor()
    {
        // Use DefaultAzureCredential for authentication
        var credential = new DefaultAzureCredential();
        _graphClient = new GraphServiceClient(credential);
    }

    public override async Task<AssessmentResult> ExecuteAsync(
        string runId,
        string tenantId,
        string scope,
        SpecCheck check,
        DiscoverySnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Build payload by fetching data from specified sources
            var payload = new JObject();

            foreach (var source in check.Sources)
            {
                var sourceData = await FetchSourceDataAsync(source, cancellationToken);
                payload.Merge(sourceData, new JsonMergeSettings 
                { 
                    MergeArrayHandling = MergeArrayHandling.Union 
                });
            }

            // Evaluate logic against payload
            var isCompliant = LogicEvaluator.Evaluate(payload, check.Logic);
            var status = isCompliant ? AssessmentStatus.Compliant : AssessmentStatus.NonCompliant;

            // Build evidence
            var evidence = BuildEvidence(check, payload);

            return new AssessmentResult(
                RunId: runId,
                TenantId: tenantId,
                Scope: scope,
                QuestionId: check.Id,
                Title: check.Title,
                Pillar: "Identity",
                Status: status,
                Severity: check.Severity,
                Coverage: "Auto",
                Evidence: evidence,
                Metadata: new Dictionary<string, object?>
                {
                    ["executor"] = Name,
                    ["sources"] = check.Sources,
                    ["payloadSize"] = payload.ToString().Length
                },
                EvaluatedAt: DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            return CreateErrorResult(runId, tenantId, scope, check, ex);
        }
    }

    private async Task<JObject> FetchSourceDataAsync(string source, CancellationToken cancellationToken)
    {
        var result = new JObject();

        try
        {
            switch (source)
            {
                case "Graph.v1.Domains":
                    var domains = await RetryPolicy.ExecuteWithRetryAsync(async () =>
                        await _graphClient.Domains.GetAsync(cancellationToken: cancellationToken), 
                        maxRetries: 3, baseDelayMs: 1000, cancellationToken);
                    result["domains"] = JArray.FromObject(domains?.Value ?? new List<Domain>());
                    break;

                case "Graph.v1.Policies":
                    // Fetch security defaults with retry
                    var policies = await RetryPolicy.ExecuteWithRetryAsync(async () =>
                        await _graphClient.Policies.IdentitySecurityDefaultsEnforcementPolicy.GetAsync(cancellationToken: cancellationToken),
                        maxRetries: 3, baseDelayMs: 1000, cancellationToken);
                    result["policies"] = new JObject
                    {
                        ["securityDefaults"] = new JObject
                        {
                            ["state"] = policies?.IsEnabled == true ? "enabled" : "disabled"
                        }
                    };
                    break;

                case "Graph.beta.ConditionalAccess":
                    // Note: This would require beta endpoint configuration
                    // For now, we'll simulate the data structure with retry protection
                    await RetryPolicy.ExecuteWithRetryAsync(async () =>
                    {
                        await Task.Delay(100, cancellationToken); // Simulate API call
                        result["caPolicies"] = new JArray();
                        return;
                    }, maxRetries: 2, baseDelayMs: 500, cancellationToken);
                    break;

                default:
                    // Log unknown source
                    result[source.Replace(".", "_")] = new JObject { ["error"] = "Unknown source" };
                    break;
            }
        }
        catch (Exception ex)
        {
            result[source.Replace(".", "_")] = new JObject { ["error"] = ex.Message };
        }

        return result;
    }

    private static Evidence BuildEvidence(SpecCheck check, JObject payload)
    {
        var summary = BuildSummary(check, payload);
        return new Evidence(
            Summary: summary,
            Links: check.Evidence.LinkTemplates);
    }

    private static string BuildSummary(SpecCheck check, JObject payload)
    {
        return check.Id switch
        {
            "ENTRA-DOMAINS-VERIFIED" => BuildDomainsSummary(payload),
            "ENTRA-SECURITY-DEFAULTS-OR-CA" => BuildSecurityDefaultsSummary(payload),
            "ENTRA-MFA-REQUIRED-ALL-USERS" => BuildMfaSummary(payload),
            _ => $"{check.Title} evaluated."
        };
    }

    private static string BuildDomainsSummary(JObject payload)
    {
        var domains = payload["domains"] as JArray ?? new JArray();
        var verified = domains.Count(d => d?["isVerified"]?.Value<bool>() == true);
        var unverified = domains.Count - verified;
        return $"Domains: {verified} verified, {unverified} unverified.";
    }

    private static string BuildSecurityDefaultsSummary(JObject payload)
    {
        var secDefaults = payload.SelectToken("$.policies.securityDefaults.state")?.ToString() ?? "unknown";
        var caPolicies = payload["caPolicies"] as JArray ?? new JArray();
        var enabledPolicies = caPolicies.Count(p => p?["state"]?.ToString() == "enabled");
        
        return $"Security Defaults: {secDefaults}; CA Policies: {enabledPolicies} enabled.";
    }

    private static string BuildMfaSummary(JObject payload)
    {
        var caPolicies = payload["caPolicies"] as JArray ?? new JArray();
        var mfaPolicies = caPolicies.Count(p => 
            p?["grantControls"]?["builtInControls"] is JArray controls &&
            controls.Any(c => c?.ToString() == "mfa"));
        
        return $"MFA policies: {mfaPolicies} found.";
    }
}