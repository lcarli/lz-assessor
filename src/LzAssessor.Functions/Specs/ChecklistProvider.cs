using System.Net.Http.Json;
using System.Text.Json;

namespace LzAssessor.Functions.Specs;

public interface IChecklistProvider
{
    Task<SpecFile> LoadAsync(string? specUrl, CancellationToken ct);
}

public class HttpChecklistProvider : IChecklistProvider
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _cfg;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public HttpChecklistProvider(IHttpClientFactory http, IConfiguration cfg)
    {
        _http = http;
        _cfg = cfg;
    }

    public async Task<SpecFile> LoadAsync(string? specUrl, CancellationToken ct)
    {
        var url = specUrl ?? _cfg["Assessment:SpecUrl"]
                  ?? throw new InvalidOperationException("SpecUrl não configurada.");

        var client = _http.CreateClient();
        using var resp = await client.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var spec = await JsonSerializer.DeserializeAsync<SpecFile>(stream, _json, ct)
                   ?? throw new InvalidOperationException("Spec inválida.");
        return spec;
    }
}