using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Theme is global static state - Theme.Apply swaps the palette for the whole process.
    /// xUnit runs test CLASSES in parallel, so any two theme-touching classes race: one applies
    /// Light, another applies Violet mid-assertion, and whichever loses reports a contrast
    /// failure that has nothing to do with the code under test.
    ///
    /// Sharing one collection forces them to run sequentially. Same reasoning as
    /// <see cref="LicenseTestCollection"/>, and like that one this is a test-isolation fix, not
    /// a product concern - the app only ever has one theme active at a time.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class ThemeTestCollection
    {
        public const string Name = "theme-global-state";
    }
}
