namespace WfmOutlookSync.Sync;

/// <summary>Fills the configurable event-subject template with values from a shift booking.</summary>
public static class SubjectTemplateFormatter
{
    public static string Format(string template, string? shiftActivityType, string? shiftPlanName, string? resourceName)
    {
        return template
            .Replace("{ShiftActivityType}", shiftActivityType ?? "Shift", StringComparison.OrdinalIgnoreCase)
            .Replace("{ShiftPlanName}", shiftPlanName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{ResourceName}", resourceName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim(' ', '\u2014', '-');
    }
}
