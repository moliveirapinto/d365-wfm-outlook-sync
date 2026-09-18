using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using WfmOutlookSync.Configuration;

namespace WfmOutlookSync.Functions;

/// <summary>Lightweight health check - confirms the app started and shows (non-secret) effective configuration.</summary>
public sealed class HttpStatusFunction
{
    private readonly SyncOptions _syncOptions;
    private readonly DataverseOptions _dataverseOptions;

    public HttpStatusFunction(IOptions<SyncOptions> syncOptions, IOptions<DataverseOptions> dataverseOptions)
    {
        _syncOptions = syncOptions.Value;
        _dataverseOptions = dataverseOptions.Value;
    }

    [Function(nameof(HttpStatusFunction))]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "status")] HttpRequest req)
    {
        return new OkObjectResult(new
        {
            status = "healthy",
            dataverseEnvironment = _dataverseOptions.EnvironmentUrl,
            dataverseAuthMode = _dataverseOptions.AuthMode.ToString(),
            lookBehindDays = _syncOptions.LookBehindDays,
            lookAheadDays = _syncOptions.LookAheadDays,
            publishedBookingStatusNames = _syncOptions.PublishedBookingStatusNames,
            dryRun = _syncOptions.DryRun
        });
    }
}
