using SiteCheck.Core.Models;
using SiteCheck.Core.Models.Stress;

namespace SiteCheck.Core.Tests.Models;

public sealed class ModelsTests
{
    [Fact]
    public void FindingSummary_WorstSeverity_FollowsRank()
    {
        FindingSummary summary = FindingSummary.From(
        [
            new Finding("a", CheckCategory.Web, Severity.Ok, "a"),
            new Finding("b", CheckCategory.Web, Severity.Warning, "b"),
            new Finding("c", CheckCategory.Mail, Severity.Critical, "c"),
        ]);

        Assert.Equal(1, summary.OkCount);
        Assert.Equal(1, summary.WarningCount);
        Assert.Equal(1, summary.CriticalCount);
        Assert.Equal(Severity.Critical, summary.WorstSeverity);
    }

    [Fact]
    public void FindingSummary_WhenEmpty_IsOk()
    {
        FindingSummary summary = FindingSummary.From([]);
        Assert.Equal(Severity.Ok, summary.WorstSeverity);
    }

    [Theory]
    [InlineData("www.beispiel.de", "beispiel.de")]
    [InlineData("beispiel.de", "beispiel.de")]
    [InlineData("a.b.c.beispiel.de", "beispiel.de")]
    [InlineData("shop.beispiel.co.uk", "beispiel.co.uk")]
    public void DomainName_GetRegistrableDomain_StripsToRegistrable(string host, string expected)
    {
        Assert.Equal(expected, DomainName.GetRegistrableDomain(host));
    }

    [Fact]
    public void ReportComparison_MarksNewResolvedAndUnchanged()
    {
        IReadOnlyList<Finding> previous =
        [
            new Finding("keep", CheckCategory.Web, Severity.Warning, "bleibt"),
            new Finding("fixed", CheckCategory.Web, Severity.Critical, "behoben"),
        ];
        IReadOnlyList<Finding> current =
        [
            new Finding("keep", CheckCategory.Web, Severity.Warning, "bleibt"),
            new Finding("new", CheckCategory.Web, Severity.Warning, "neu"),
        ];

        IReadOnlyList<ComparedFinding> compared = ReportComparison.Compare(current, previous);

        Assert.Equal(FindingChange.Unchanged, compared.Single(c => c.Finding.Key == "keep").Change);
        Assert.Equal(FindingChange.New, compared.Single(c => c.Finding.Key == "new").Change);
        Assert.Equal(FindingChange.Resolved, compared.Single(c => c.Finding.Key == "fixed").Change);
    }

    [Fact]
    public void StressTestOptions_Clamp_EnforcesCeilings()
    {
        StressTestOptions clamped = new StressTestOptions(1000, TimeSpan.FromHours(1)).Clamp();

        Assert.Equal(StressTestOptions.MaxConcurrency, clamped.Concurrency);
        Assert.Equal(StressTestOptions.MaxDurationSeconds, clamped.Duration.TotalSeconds);
    }

    [Fact]
    public void StressTestOptions_Clamp_EnforcesFloors()
    {
        StressTestOptions clamped = new StressTestOptions(0, TimeSpan.Zero).Clamp();

        Assert.Equal(StressTestOptions.MinConcurrency, clamped.Concurrency);
        Assert.Equal(StressTestOptions.MinDurationSeconds, clamped.Duration.TotalSeconds);
    }

    [Fact]
    public void StressTestResult_From_ComputesRatesAndQuantiles()
    {
        RequestOutcome[] outcomes =
        [
            new(100, 200),
            new(200, 200),
            new(300, 500),
            new(400, null),
        ];

        StressTestResult result = StressTestResult.From(outcomes, TimeSpan.FromSeconds(2));

        Assert.Equal(4, result.TotalRequests);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(2, result.FailureCount);
        Assert.Equal(0.5, result.FailureRate);
        Assert.Equal(2, result.RequestsPerSecond);
        Assert.Equal(250, result.AverageMilliseconds);
    }

    [Fact]
    public void StressTestResult_From_WhenEmpty_IsAllZero()
    {
        StressTestResult result = StressTestResult.From([], TimeSpan.FromSeconds(1));
        Assert.Equal(0, result.TotalRequests);
        Assert.Equal(0, result.RequestsPerSecond);
    }
}
