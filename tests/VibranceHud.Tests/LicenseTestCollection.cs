using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Every license test reads and writes the one real license file at
    /// %LocalAppData%\PlexusX\license.json - LicenseService hardcodes that path.
    /// xUnit runs separate test CLASSES in parallel, so without this collection the
    /// license classes race each other over that single file and TryActivate starts
    /// returning Invalid (its write-failure result) at random.
    ///
    /// Sharing one collection forces them to run sequentially. Note this is a test
    /// isolation fix, not a product fix: the same contention is real if two copies
    /// of PlexusX ever run at once, which is what <see cref="VibranceHud.SingleInstance"/>
    /// now prevents.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class LicenseTestCollection
    {
        public const string Name = "license-file";
    }
}
