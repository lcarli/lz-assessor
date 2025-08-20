using System.Net.Http.Headers;
using Azure.Core;
using Azure.Identity;
using Newtonsoft.Json.Linq;

namespace LzAssessor.Functions.Engine;

public sealed class GraphHttp
{
    private readonly HttpClient _http;
    private readonly TokenCredential _cred;
    private const string Scope = "https://graph.microsoft.com/.default";
    private const string BaseV1 = "https://graph.microsoft.com/v1.0";
    private const string BaseBeta = "https://graph.microsoft.com/beta";

    public GraphHttp(IHttpClientFactory httpFactory)
    {
        _http = httpFactory.CreateClient();
        _cred = new DefaultAzureCredential();
    }

    private async Task SetAuthAsync()
    {
        var token = await _cred.GetTokenAsync(
            new TokenRequestContext(new[] { Scope }),
            CancellationToken.None);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
    }

    public async Task<JObject> GetDomainsAsync()
    {
        await SetAuthAsync();
        var url = $"{BaseV1}/domains";
        var json = await _http.GetStringAsync(url);
        var jobj = JObject.Parse(json);
        return new JObject { ["domains"] = jobj["value"] ?? new JArray() };
    }

    public async Task<JObject> GetSecurityDefaultsAsync()
    {
        await SetAuthAsync();
        var url = $"{BaseV1}/policies/identitySecurityDefaultsEnforcementPolicy";
        var json = await _http.GetStringAsync(url);
        var obj = JObject.Parse(json);
        return new JObject
        {
            ["policies"] = new JObject
            {
                ["securityDefaults"] = new JObject
                {
                    ["state"] = (string?)obj["isEnabled"] == "True" || (bool?)obj["isEnabled"] == true ? "enabled" : "disabled"
                }
            }
        };
    }

    public async Task<JObject> GetConditionalAccessPoliciesAsync()
    {
        await SetAuthAsync();
        // CA policies estão no beta: /beta/identity/conditionalAccess/policies
        var url = $"{BaseBeta}/identity/conditionalAccess/policies";
        var json = await _http.GetStringAsync(url);
        var jobj = JObject.Parse(json);
        return new JObject { ["caPolicies"] = jobj["value"] ?? new JArray() };
    }
}