using LzAssessor.Functions.Models;
using LzAssessor.Functions.Specs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LzAssessor.Functions.Engine;

public sealed class GraphExecutor : IExecutor
{
    public string Name => "graph";
    private readonly GraphHttp _graph;

    public GraphExecutor(GraphHttp graph) => _graph = graph;

    public async Task<AssessmentResult> ExecuteAsync(
        string runId,
        string tenantId,
        string scope,
        SpecCheck check,
        DiscoverySnapshot snapshot,
        CancellationToken ct)
    {
        // 1) Monta payload combinando as fontes declaradas
        var payload = new JObject();

        foreach (var src in check.Sources ?? Array.Empty<string>())
        {
            var part = await FetchSourceAsync(src);
            payload.Merge(part, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Union });
        }

        // 2) Avalia a lógica declarativa
        //    check.Logic (System.Text.Json) -> Newtonsoft JObject
        var logic = check.Logic is null ? new JObject() : JObject.Parse(JsonConvert.SerializeObject(check.Logic));
        var ok = LogicEvaluator.Evaluate(payload, logic);

        // 3) Monta evidência simples (pode ficar mais rica por check)
        var (summary, links) = BuildEvidence(check, payload);

        return new AssessmentResult(
            RunId: runId,
            TenantId: tenantId,
            Scope: scope,
            QuestionId: check.Id,
            Title: check.Title,
            Pillar: "Billing/Entra",
            Status: ok ? "Compliant" : "NonCompliant",
            Severity: check.Severity,
            Coverage: "Auto",
            Evidence: new Evidence(summary, links, RawRef: null),
            Metadata: new Dictionary<string, object?>
            {
                ["executor"] = Name,
                ["sources"] = check.Sources,
            }!.ToDictionary(k => k.Key, v => (object)v.Value!),
            UpdatedAt: DateTimeOffset.UtcNow
        );
    }

    private async Task<JObject> FetchSourceAsync(string src) =>
        src switch
        {
            "Graph.v1.Domains" => await _graph.GetDomainsAsync(),
            "Graph.v1.Policies" => await _graph.GetSecurityDefaultsAsync(),
            "Graph.beta.ConditionalAccess" => await _graph.GetConditionalAccessPoliciesAsync(),
            _ => new JObject()
        };

    private static (string Summary, IEnumerable<string>? Links) BuildEvidence(SpecCheck check, JObject payload)
    {
        // Pequenos enriquecimentos automáticos para alguns checks comuns
        var links = check.Evidence.LinkTemplates;

        if (check.Id == "ENTRA-DOMAINS-VERIFIED")
        {
            var domains = (payload["domains"] as JArray) ?? [];
            var verified = domains.Count(d => d?["isVerified"]?.Value<bool>() == true);
            var unverified = domains.Count - verified;
            var s = $"Domínios verificados: {verified}; não verificados: {unverified}.";
            return (s, links);
        }

        if (check.Id == "ENTRA-SECURITY-DEFAULTS-OR-CA")
        {
            var sec = payload.SelectToken("$.policies.securityDefaults.state")?.ToString() ?? "unknown";
            var caEnabled = (payload["caPolicies"] as JArray)?.Count(p => p?["state"]?.ToString() == "enabled") ?? 0;
            var s = $"Security Defaults: {sec}; CA habilitadas: {caEnabled}.";
            return (s, links);
        }

        if (check.Id == "ENTRA-MFA-REQUIRED-ALL-USERS")
        {
            var ca = (payload["caPolicies"] as JArray) ?? [];
            var globalMfa = ca.Any(p =>
            {
                var state = p?["state"]?.ToString() == "enabled";
                var grants = p?["grantControls"]?["builtInControls"] as JArray;
                var hasMfa = grants != null && grants.Any(x => x?.ToString().Equals("mfa", StringComparison.OrdinalIgnoreCase) == true);
                var includesAll = p?["conditions"]?["users"]?["includeUsers"] is JArray inc && inc.Any(x => x?.ToString() == "All");
                return state && hasMfa && includesAll;
            });
            var exclusions = ca.SelectMany(p => p?["conditions"]?["users"]?["excludeUsers"] as JArray ?? new JArray()).Count();
            var s = $"MFA global por CA: {(globalMfa ? "sim" : "não")}; exclusões totais: {exclusions}.";
            return (s, links);
        }

        // fallback genérico
        return ($"{check.Title} avaliado.", links);
    }
}