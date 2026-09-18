using System.Text.Json;

namespace WfmOutlookSync.Dataverse;

/// <summary>Convenience readers for Dataverse Web API JSON rows, including formatted-value annotations.</summary>
internal static class JsonElementExtensions
{
    public static string? GetStringOrNull(this JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;

    public static Guid? GetGuidOrNull(this JsonElement element, string property)
    {
        var s = element.GetStringOrNull(property);
        return s is not null && Guid.TryParse(s, out var g) ? g : null;
    }

    public static Guid GetGuid(this JsonElement element, string property) =>
        element.GetGuidOrNull(property) ?? throw new InvalidOperationException($"Property '{property}' was expected to be a GUID.");

    public static DateTimeOffset? GetDateTimeOffsetOrNull(this JsonElement element, string property)
    {
        var s = element.GetStringOrNull(property);
        return s is not null && DateTimeOffset.TryParse(s, null, System.Globalization.DateTimeStyles.AssumeUniversal, out var dt)
            ? dt
            : null;
    }

    public static int GetIntOrDefault(this JsonElement element, string property, int defaultValue = 0) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : defaultValue;

    public static bool GetBoolOrDefault(this JsonElement element, string property, bool defaultValue = false) =>
        element.TryGetProperty(property, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : defaultValue;

    /// <summary>Reads the human-readable "@OData.Community.Display.V1.FormattedValue" annotation for a lookup/option-set field.</summary>
    public static string? GetFormattedValueOrNull(this JsonElement element, string property) =>
        element.GetStringOrNull(property + "@OData.Community.Display.V1.FormattedValue");
}
