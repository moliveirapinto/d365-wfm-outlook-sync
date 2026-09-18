using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using WfmOutlookSync.Sync;

namespace WfmOutlookSync.Functions;

/// <summary>
/// On-demand HTTP trigger for running a sync outside of the timer schedule - useful right
/// after deployment, or for wiring into an external orchestrator. Protected by a function key.
/// POST /api/sync           - normal incremental sync
/// POST /api/sync?force=true - re-pushes every eligible booking even if unchanged (self-heal)
/// </summary>
public sealed class HttpSyncFunction
{
    private readonly ShiftSyncEngine _engine;

    public HttpSyncFunction(ShiftSyncEngine engine)
    {
        _engine = engine;
    }

    [Function(nameof(HttpSyncFunction))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "sync")] HttpRequest req,
        CancellationToken ct)
    {
        var force = req.Query.TryGetValue("force", out var v) && bool.TryParse(v, out var b) && b;

        var result = await _engine.RunAsync(force, ct);

        return new OkObjectResult(result);
    }
}
