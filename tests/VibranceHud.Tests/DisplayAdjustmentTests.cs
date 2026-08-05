using System;
using System.Collections.Generic;
using System.IO;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class DisplayAdjustmentTests
    {
        private static float At(float[] matrix, int row, int column) => matrix[row * 5 + column];

        [Fact]
        public void Contrast_IsAGainAroundMidGrey()
        {
            var matrix = ColorAdjust.Build(1f, 1f, 1.2f, 1f, 0f);

            Assert.Equal(1.2f, At(matrix, 0, 0), 4);
            Assert.Equal(1.2f, At(matrix, 1, 1), 4);
            Assert.Equal(1.2f, At(matrix, 2, 2), 4);
            Assert.Equal(-0.1f, At(matrix, 4, 0), 4);
            Assert.Equal(-0.1f, At(matrix, 4, 1), 4);
            Assert.Equal(-0.1f, At(matrix, 4, 2), 4);
        }

        [Fact]
        public void CoolTemperature_IsTheMirrorOfWarmTemperature()
        {
            var warm = ColorAdjust.Build(1f, 1f, 1f, 1f, 0.5f);
            var cool = ColorAdjust.Build(1f, 1f, 1f, 1f, -0.5f);

            Assert.Equal(1f, At(warm, 0, 0), 4);
            Assert.True(At(warm, 2, 2) < At(warm, 0, 0));
            Assert.Equal(1f, At(cool, 2, 2), 4);
            Assert.True(At(cool, 0, 0) < At(cool, 2, 2));
        }

        [Fact]
        public void ContrastAndTemperatureArePartOfIdentityDetection()
        {
            Assert.True(ColorAdjust.IsIdentity(1f, 1f, 1f, 1f, 0f));
            Assert.False(ColorAdjust.IsIdentity(1f, 1f, 1.01f, 1f, 0f));
            Assert.False(ColorAdjust.IsIdentity(1f, 1f, 1f, 1f, -0.01f));
        }

        [Fact]
        public void Settings_RoundTripContrastAndTemperature()
        {
            string dir = Path.Combine(Path.GetTempPath(), "PlexusXDisplay_" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new SettingsStore(dir);
                store.Save(new AppSettings { ContrastPercent = 114, Temperature = -23 });

                var loaded = store.Load();
                Assert.Equal(114, loaded.ContrastPercent);
                Assert.Equal(-23, loaded.ResolvedTemperature);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void OldEyeCareSettingMigratesToTheEquivalentTemperature()
        {
            Assert.Equal(VibranceEngine.EyeCareTemperature,
                new AppSettings { EyeCare = true }.ResolvedTemperature);
            Assert.Equal(0, new AppSettings { EyeCare = false }.ResolvedTemperature);
        }

        [Fact]
        public void Engine_ClampsAndResetsNewControls()
        {
            var controller = new Controller();
            var overlay = new Overlay();
            var engine = new VibranceEngine(controller, overlay, new Gamma());

            engine.Contrast = 999;
            engine.Temperature = -999;
            Assert.Equal(VibranceEngine.MaxContrast, engine.Contrast);
            Assert.Equal(VibranceEngine.MinTemperature, engine.Temperature);

            engine.Reset();
            Assert.Equal(100, engine.Contrast);
            Assert.Equal(0, engine.Temperature);
        }

        private sealed class Controller : IVibranceController
        {
            public int CurrentLevel { get; set; } = 50;
            public int DefaultLevel => 50;
            public bool IsAvailable => true;
            public void SetLevel(int level) => CurrentLevel = level;
        }

        private sealed class Overlay : ISaturationOverlay
        {
            public List<float[]> Applied { get; } = new();
            public void Apply(float[] matrix) => Applied.Add(matrix);
            public void Clear() { }
        }

        private sealed class Gamma : IGammaRamp
        {
            public void Apply(ushort[] ramp) { }
            public void Reset() { }
        }
    }
}
