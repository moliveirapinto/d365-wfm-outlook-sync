using WfmOutlookSync.Sync;
using Xunit;

namespace WfmOutlookSync.Tests;

public class SubjectTemplateFormatterTests
{
    [Fact]
    public void Format_ReplacesAllTokens()
    {
        var result = SubjectTemplateFormatter.Format(
            "{ShiftActivityType} \u2014 {ShiftPlanName}", "Voice calls", "August 2026 Schedule", "Alan Steiner");

        Assert.Equal("Voice calls \u2014 August 2026 Schedule", result);
    }

    [Fact]
    public void Format_MissingValues_FallsBackGracefully()
    {
        var result = SubjectTemplateFormatter.Format("{ShiftActivityType} \u2014 {ShiftPlanName}", null, null, null);

        Assert.Equal("Shift", result);
    }

    [Fact]
    public void Format_ResourceNameToken_IsSubstituted()
    {
        var result = SubjectTemplateFormatter.Format("{ResourceName}: {ShiftActivityType}", "Break", "Plan", "Jane Doe");

        Assert.Equal("Jane Doe: Break", result);
    }
}
