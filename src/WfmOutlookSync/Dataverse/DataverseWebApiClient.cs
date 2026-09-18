using System.Net.Http.Json;
using System.Text.Json;

namespace WfmOutlookSync.Dataverse;

/// <summary>
/// Minimal Dataverse Web API (OData v4) client: paginated GET with automatic
/// @odata.nextLink following, and a helper to build large "IN" filters safely.
/// </summary>
public sealed class DataverseWebApiClient
{
    private readonly HttpClient _http;

    public DataverseWebApiClient(HttpClient http)
    {
        _http = http;
        _http.DefaultRequestHeaders.Accept.Clear();
        _http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        _http.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
        _http.DefaultRequestHeaders.Add("OData-Version", "4.0");
        _http.DefaultRequestHeaders.Add("Prefer", "odata.include-annotations=\"*\"");
    }

    /// <summary>Fetches every row for a query, following @odata.nextLink pages.</summary>
    public async Task<List<JsonElement>> GetAllAsync(string relativeOrAbsoluteUrl, CancellationToken ct)
    {
        var results = new List<JsonElement>();
        string? next = relativeOrAbsoluteUrl;

        while (next is not null)
        {
            using var response = await _http.GetAsync(next, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new DataverseRequestException(
                    $"Dataverse request failed ({(int)response.StatusCode} {response.StatusCode}) for '{next}': {body}");
            }

            var doc = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct)
                       ?? throw new DataverseRequestException($"Empty response body from '{next}'.");

            if (doc.RootElement.TryGetProperty("value", out var values))
            {
                results.AddRange(values.EnumerateArray());
            }

            next = doc.RootElement.TryGetProperty("@odata.nextLink", out var nextLink)
                ? nextLink.GetString()
                : null;
        }

        return results;
    }

    /// <summary>
    /// Builds a Dataverse "IN" filter clause across possibly-large ID sets, chunked to stay
    /// comfortably under URL length limits, e.g. for use with <see cref="CombineOrFilters"/>.
    /// </summary>
    public static IEnumerable<string> BuildInFilterChunks(string propertyName, IEnumerable<Guid> values, int chunkSize = 100)
    {
        var list = values.Distinct().ToList();
        for (var i = 0; i < list.Count; i += chunkSize)
        {
            var chunk = list.Skip(i).Take(chunkSize).Select(g => $"'{g:D}'");
            yield return $"Microsoft.Dynamics.CRM.In(PropertyName='{propertyName}',PropertyValues=[{string.Join(",", chunk)}])";
        }
    }
}

public sealed class DataverseRequestException : Exception
{
    public DataverseRequestException(string message) : base(message)
    {
    }
}
