using System;
using VibranceHud.Licensing;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class PlanCatalogTests
    {
        [Fact] public void TrialLastsFourDays() =>
            Assert.Equal(TimeSpan.FromDays(4), PlanCatalog.DurationFor(PlanCatalog.Trial));

        [Fact] public void MonthlyLastsThirtyDays() =>
            Assert.Equal(TimeSpan.FromDays(30), PlanCatalog.DurationFor(PlanCatalog.Monthly));

        [Fact] public void Lifetime600LastsSixHundredDays() =>
            Assert.Equal(TimeSpan.FromDays(600), PlanCatalog.DurationFor(PlanCatalog.Lifetime600));

        /// <summary>An unknown plan must not resolve to a duration. Defaulting here is how the
        /// beta accidentally granted a year to unrecognised keys.</summary>
        [Theory]
        [InlineData("")]
        [InlineData("premium")]
        [InlineData("MONTHLY")]
        [InlineData(null)]
        public void UnknownPlanHasNoDuration(string? planId)
        {
            Assert.Null(PlanCatalog.DurationFor(planId));
            Assert.False(PlanCatalog.IsKnown(planId));
        }

        [Fact]
        public void KnownPlansReportAsKnown()
        {
            Assert.True(PlanCatalog.IsKnown(PlanCatalog.Trial));
            Assert.True(PlanCatalog.IsKnown(PlanCatalog.Monthly));
            Assert.True(PlanCatalog.IsKnown(PlanCatalog.Lifetime600));
        }

        /// <summary>Plan ids are written into signed licences, so renaming one would
        /// invalidate every licence issued under it.</summary>
        [Fact]
        public void PlanIdsAreStable()
        {
            Assert.Equal("trial", PlanCatalog.Trial);
            Assert.Equal("monthly", PlanCatalog.Monthly);
            Assert.Equal("lifetime600", PlanCatalog.Lifetime600);
        }
    }
}
