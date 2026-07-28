using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using VibranceHud.SystemTweaks;

namespace VibranceHud.Nvidia
{
    /// <summary>
    /// The slice of NVIDIA Experience's per-game profile JSON we care about.
    /// Schema was reverse-engineered from the file NVIDIA App writes under
    /// <c>%LOCALAPPDATA%\NVIDIA Corporation\NVIDIA App\NvBackend\ApplicationState\&lt;LocalId&gt;.json</c>:
    /// <code>{ "EverChangedByGFE":true, "CurrentPowerMode":0, "CurrentDCState":0,
    ///        "TargetPowerMode":0, "TargetDCState":0 }</code>
    /// The <c>Target*</c> values are what NVIDIA Experience should apply next time
    /// Rust launches; <c>Current*</c> are what is active now.
    /// </summary>
    public sealed class NvAppRustProfile
    {
        [JsonPropertyName("EverChangedByGFE")] public bool EverChangedByGFE { get; set; }
        [JsonPropertyName("CurrentPowerMode")] public int CurrentPowerMode { get; set; }
        [JsonPropertyName("CurrentDCState")]  public int CurrentDCState  { get; set; }
        [JsonPropertyName("TargetPowerMode")]  public int TargetPowerMode  { get; set; }
        [JsonPropertyName("TargetDCState")]   public int TargetDCState   { get; set; }
    }

    /// <summary>
    /// Sets NVIDIA Experience's per-game "Performance" slider for Rust to Potato
    /// (the leftmost position = driver-side downscaling). This is the slider NVIDIA
    /// App shows under the per-game "Optimal settings" page; it is NOT the same as
    /// Rust's own graphics quality dropdown. The point of this tweak is that
    /// NVIDIA's slider downscales the rendered image inside the driver, which
    /// typically gives more FPS than telling Rust to render fewer pixels because
    /// Rust's own scaler is friendlier to UI/HUD sharpness than the driver's.
    ///
    /// Lives in <c>%LOCALAPPDATA%\NVIDIA Corporation\NVIDIA App\NvBackend\</c> -
    /// per-user, no UAC, reversible. If NVIDIA Experience is not installed or
    /// the per-game file is missing, IsApplied/Apply/Revert all degrade gracefully
    /// (missing file = "not applied"; apply writes a fresh file; revert leaves the
    /// file absent).
    /// </summary>
    public sealed class NvAppRustProfileTweak : ISystemTweak
    {
        /// <summary>Rust's LocalId in NVIDIA Experience's ApplicationStorage.json.
        /// Hardcoded as a known-good fallback so the tweak still works when NVIDIA
        /// hasn't been launched yet to populate the storage file.</summary>
        internal const string KnownRustLocalId = "761841999";

        /// <summary>0 = leftmost "Performance" position on NVIDIA Experience's
        /// per-game slider. 1, 2, 3 are progressively higher quality.</summary>
        internal const int PotatoPreset = 0;
        /// <summary>One notch up from Potato. NVIDIA Experience's stock
        /// "Recommended" position; what Revert writes back so the user
        /// isn't left on the worst-looking preset.</summary>
        internal const int DefaultPreset = 1;

        private readonly string _baseDir;
        private readonly string? _localIdOverride;

        /// <summary>Production ctor: uses the real NVIDIA App folder under
        /// %LOCALAPPDATA%. Looks up Rust's LocalId dynamically, falls back
        /// to <see cref="KnownRustLocalId"/> when NVIDIA hasn't been launched.</summary>
        public NvAppRustProfileTweak()
            : this(DefaultBaseDir, localIdOverride: null) { }

        /// <summary>Test-friendly ctor: writes to an isolated directory so
        /// tests don't touch the real NVIDIA App state. <paramref name="localIdOverride"/>
        /// skips the ApplicationStorage.json lookup when non-null (used by the
        /// catalog-level tests where we don't want to fabricate a storage file).</summary>
        public NvAppRustProfileTweak(string baseDir, string? localIdOverride)
        {
            _baseDir = baseDir;
            _localIdOverride = localIdOverride;
        }

        private static string DefaultBaseDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"NVIDIA Corporation\NVIDIA App\NvBackend\ApplicationState");

        // The lookup file lives one level up from ApplicationState, in NvBackend itself.
        // baseDir = ...\NvBackend\ApplicationState, so the parent is ...\NvBackend.
        private static string StorageJsonPath(string baseDir) =>
            Path.Combine(Path.GetDirectoryName(baseDir)!, "ApplicationStorage.json");

        public string Id => "nvapp-rust-potato";
        public string Label => "Potato (NVIDIA Experience)";
        public string Description =>
            "Sets NVIDIA Experience's Rust profile to Potato via the Performance slider. " +
            "Lower image quality without touching Rust's own graphics settings - usually " +
            "gives more FPS than the in-game slider because the image is downscaled at the driver level.";
        public string Category => "NVIDIA";
        public TweakTier Tier => TweakTier.Safe;
        public bool RequiresAdmin => false;

        public bool IsApplied()
        {
            try
            {
                var path = ProfilePath();
                if (!File.Exists(path)) return false;
                var profile = ReadProfile(path);
                // The Target fields are what NVIDIA Experience will apply next launch;
                // treat TargetPowerMode == 0 as "user has potato selected for this game".
                return profile?.TargetPowerMode == PotatoPreset
                    && profile?.TargetDCState == PotatoPreset;
            }
            catch
            {
                return false;
            }
        }

        public SystemTweakResult Apply()
        {
            var path = ProfilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var profile = ReadProfile(path) ?? new NvAppRustProfile();
            profile.EverChangedByGFE = true;
            profile.CurrentPowerMode = PotatoPreset;
            profile.CurrentDCState = PotatoPreset;
            profile.TargetPowerMode = PotatoPreset;
            profile.TargetDCState = PotatoPreset;
            WriteProfile(path, profile);
            return new SystemTweakResult(true, "NVIDIA Experience set to Potato");
        }

        public void Revert()
        {
            var path = ProfilePath();
            if (!File.Exists(path)) return;
            var profile = ReadProfile(path) ?? new NvAppRustProfile();
            profile.EverChangedByGFE = true;
            profile.CurrentPowerMode = DefaultPreset;
            profile.CurrentDCState = DefaultPreset;
            profile.TargetPowerMode = DefaultPreset;
            profile.TargetDCState = DefaultPreset;
            WriteProfile(path, profile);
        }

        /// <summary>The absolute path to the Rust ApplicationState JSON. Public
        /// for tests; production callers use the IsApplied/Apply/Revert surface.</summary>
        internal string ProfilePath()
        {
            var localId = _localIdOverride ?? ResolveLocalId(_baseDir);
            return Path.Combine(_baseDir, localId + ".json");
        }

        private static string ResolveLocalId(string baseDir)
        {
            // ApplicationStorage.json sits next to ApplicationState/; the storage
            // file maps "Rust" to a LocalId we then read under ApplicationState/.
            // If anything goes wrong (no NVIDIA App, no Rust entry yet) we fall
            // back to the hardcoded known-good id so the tweak still works.
            try
            {
                var storagePath = StorageJsonPath(baseDir);
                if (!File.Exists(storagePath)) return KnownRustLocalId;
                using var stream = File.OpenRead(storagePath);
                using var doc = JsonDocument.Parse(stream);
                if (!doc.RootElement.TryGetProperty("KnownApplications", out var apps)
                    && !doc.RootElement.TryGetProperty("Applications", out apps))
                    return KnownRustLocalId;

                foreach (var app in apps.EnumerateArray())
                {
                    var name = TryGetString(app, "Name") ?? TryGetString(app, "AppName");
                    if (string.Equals(name, "Rust", StringComparison.OrdinalIgnoreCase))
                        return TryGetString(app, "LocalId") ?? KnownRustLocalId;
                }
            }
            catch
            {
                /* fall through to fallback */
            }
            return KnownRustLocalId;
        }

        private static string? TryGetString(JsonElement el, string name) =>
            el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;

        private static NvAppRustProfile? ReadProfile(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                using var stream = File.OpenRead(path);
                return JsonSerializer.Deserialize<NvAppRustProfile>(stream);
            }
            catch
            {
                // Corrupt or unreadable JSON: treat as "no current state" so the next
                // Apply/Revert call just overwrites the file.
                return null;
            }
        }

        private static void WriteProfile(string path, NvAppRustProfile profile)
        {
            // Write to a sibling .tmp then move into place, so a partial write
            // never leaves NVIDIA Experience looking at a corrupt JSON.
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(profile));
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }
    }
}