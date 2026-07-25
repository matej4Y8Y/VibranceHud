using System;
using System.Drawing;
using System.IO;

namespace VibranceHud.Theming
{
    /// <summary>
    /// Ties a picked image to the live theme: copies it somewhere safe, extracts the
    /// accent, installs the palette, and restores all of that on the next launch.
    ///
    /// Every path here is non-destructive by design - a missing, locked or corrupt image
    /// leaves the current theme alone rather than throwing or blanking the UI.
    /// </summary>
    public sealed class CustomThemeService
    {
        private const string FileName = "background.png";

        private readonly string _dataDir;
        private readonly AppSettings _settings;

        public CustomThemeService(string dataDir, AppSettings settings)
        {
            _dataDir = dataDir;
            _settings = settings;
        }

        public string ImagePath => Path.Combine(_dataDir, FileName);

        public bool HasImage => !string.IsNullOrEmpty(_settings.CustomBackgroundFile)
                                && File.Exists(ImagePath);

        /// <summary>
        /// Adopt a new image: copy it into the app's own folder, derive the accent, and
        /// install the palette. Returns false and changes nothing if it can't be read.
        /// </summary>
        public bool SetImage(string sourcePath)
        {
            try
            {
                Directory.CreateDirectory(_dataDir);

                // Re-encode rather than File.Copy: it normalises whatever format the user
                // picked, and proves the file is actually a readable image before we
                // commit any of it to settings.
                using (var fs = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
                using (var img = Image.FromStream(fs))
                using (var copy = new Bitmap(img))
                    copy.Save(ImagePath, System.Drawing.Imaging.ImageFormat.Png);
            }
            catch
            {
                return false;
            }

            if (!AppBackground.Load(ImagePath, _settings.CustomBackgroundDim, _settings.CustomBackgroundBlur)) return false;

            var theme = ImagePalette.Extract(AppBackground.SamplePixels(),
                                             ThemeCatalog.ByName(ThemeCatalog.DefaultName).Accent);

            _settings.CustomBackgroundFile = FileName;
            _settings.CustomBackgroundDim = theme.SuggestedDim;
            _settings.CustomAccentArgb = theme.Accent.ToArgb();

            AppBackground.SetDim(theme.SuggestedDim);
            Install(theme);
            return true;
        }

        /// <summary>Re-apply a previously saved image theme at startup, using the cached
        /// accent so the image doesn't have to be re-scanned.</summary>
        public bool Restore()
        {
            if (!HasImage) return false;
            if (!AppBackground.Load(ImagePath, _settings.CustomBackgroundDim, _settings.CustomBackgroundBlur)) return false;

            var accent = _settings.CustomAccentArgb != 0
                ? Color.FromArgb(_settings.CustomAccentArgb)
                : ImagePalette.Extract(AppBackground.SamplePixels(),
                    ThemeCatalog.ByName(ThemeCatalog.DefaultName).Accent).Accent;

            Install(ImagePalette.Derive(accent, _settings.CustomBackgroundDim));
            return true;
        }

        public void SetDim(int dim)
        {
            _settings.CustomBackgroundDim = dim;
            AppBackground.SetDim(dim);
        }

        public void SetBlur(int blur)
        {
            _settings.CustomBackgroundBlur = blur;
            AppBackground.SetBlur(blur);
        }

        /// <summary>Drop the custom background and its palette.</summary>
        public void Remove()
        {
            AppBackground.Clear();
            ThemeCatalog.ClearCustom();
            _settings.CustomBackgroundFile = "";
            _settings.CustomAccentArgb = 0;
            try { if (File.Exists(ImagePath)) File.Delete(ImagePath); } catch { /* best effort */ }
        }

        private static void Install(ImageTheme t) => ThemeCatalog.SetCustom(t);
    }
}
