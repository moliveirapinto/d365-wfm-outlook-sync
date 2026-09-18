using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using WfmOutlookSync.Sync;

namespace WfmOutlookSync.Functions;

public sealed class TimerSyncFunction
{
    private readonly ShiftSyncEngine _engine;
    private readonly ILogger<TimerSyncFunction> _logger;

    public TimerSyncFunction(ShiftSyncEngine engine, ILogger<TimerSyncFunction> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    /// <summary>
    /// Runs the sync on a recurring schedule. The schedule itself is fully configurable via the
    /// "SYNC_CRON_SCHEDULE" app setting (an NCRONTAB expression, e.g. "0 */5 * * * *" for every
    /// 5 minutes) - change it and restart the app to sync more or less often; no code change or
    /// redeploy required.
    /// </summary>
    [Function(nameof(TimerSyncFunction))]
    public async Task Run([TimerTrigger("%SYNC_CRON_SCHEDULE%")] TimerInfo timer, CancellationToken ct)
    {
        _logger.LogInformation("WFM -> Outlook sync starting (next scheduled run: {Next})", timer.ScheduleStatus?.Next);

        var result = await _engine.RunAsync(ct);

        _logger.LogInformation(
            "WFM -> Outlook sync completed in {DurationMs}ms. Evaluated={Evaluated} Created={Created} Updated={Updated} " +
            "Deleted={Deleted} SkippedNoMailbox={SkippedNoMailbox} SkippedUnpublished={SkippedUnpublished} Failures={Failures} DryRun={DryRun}",
            result.Duration.TotalMilliseconds, result.BookingsEvaluated, result.EventsCreated, result.EventsUpdated,
            result.EventsDeleted, result.EventsSkippedNoMailbox, result.EventsSkippedUnpublished, result.Failures, result.DryRun);

        foreach (var error in result.Errors)
        {
            _logger.LogWarning("Sync error: {Error}", error);
        }
    }
}
