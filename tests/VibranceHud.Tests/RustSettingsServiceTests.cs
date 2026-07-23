using System;
using System.Collections.Generic;
using System.IO;
using VibranceHud.Rust;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class RustSettingsServiceTests : IDisposable
    {
        private readonly string _dir;
        private readonly string _cfg;
        private const string Original =
            "fps.limit \"144\"\n" +
            "graphics.quality \"5\"\n" +
            "effects.motionblur \"True\"\n";

        public RustSettingsServiceTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "RustSvcTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _cfg = Path.Combine(_dir, "client.cfg");
            File.WriteAllText(_cfg, Original);
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        [Fact]
        public void Apply_WritesChanges_AndBacksUpOriginal()
        {
            var svc = new RustSettingsService(_cfg);

            svc.Apply(new Dictionary<string, string> { ["fps.limit"] = "240", ["effects.motionblur"] = "False" });

            Assert.Equal("240", svc.ReadCurrent().Get("fps.limit"));
            Assert.Equal("False", svc.ReadCurrent().Get("effects.motionblur"));
            Assert.True(svc.HasBackup);
            Assert.Equal(Original, File.ReadAllText(svc.BackupPath)); // pristine original saved
        }

        [Fact]
        public void Apply_Twice_BackupStaysPristine()
        {
            var svc = new RustSettingsService(_cfg);

            svc.Apply(new Dictionary<string, string> { ["fps.limit"] = "240" });
            svc.Apply(new Dictionary<string, string> { ["fps.limit"] = "60" });

            Assert.Equal("60", svc.ReadCurrent().Get("fps.limit"));
            Assert.Equal(Original, File.ReadAllText(svc.BackupPath)); // still the very first original
        }

        [Fact]
        public void Restore_RevertsToOriginal()
        {
            var svc = new RustSettingsService(_cfg);
            svc.Apply(new Dictionary<string, string> { ["fps.limit"] = "240" });

            svc.Restore();

            Assert.Equal(Original, File.ReadAllText(_cfg));
            Assert.Equal("144", svc.ReadCurrent().Get("fps.limit"));
        }
    }
}
