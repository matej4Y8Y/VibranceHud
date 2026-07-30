using System;
using System.IO;

namespace VibranceHud.Tests
{
    /// <summary>
    /// A throwaway directory for a test's license.json.
    ///
    /// The licence tests used to run against the one real file in
    /// %LocalAppData%\PlexusX, and their cleanup calls Deactivate(), which DELETES it. So
    /// running the suite silently signed the developer out of their own app and left the next
    /// launch sitting on the activation dialog - which then looked like an unrelated bug,
    /// because settings.json went stale and reported the wrong display engine. That misdirected
    /// real debugging time more than once.
    ///
    /// Wrap each test's LicenseService in one of these and nothing outside the temp folder is
    /// touched.
    /// </summary>
    public sealed class TempLicenseDir : IDisposable
    {
        public string Path { get; }

        public TempLicenseDir()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "plexusx-lic-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string LicenseFile => System.IO.Path.Combine(Path, "license.json");

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { /* best effort */ }
        }
    }
}
