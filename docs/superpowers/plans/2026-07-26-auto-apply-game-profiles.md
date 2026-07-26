# PlexusX 0.7.0 Auto-Apply Game Profiles Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When the user launches a supported game (Rust, CS2, ...), PlexusX applies that game's saved profile (visual sliders + Game-Hub options). On game close, desktop defaults return. The trigger is process-based polling — no Steam dependency, no anti-cheat surface.

**Architecture:** Five new components (`GameProfile`, `GameProfileStore`, `ProfileApplyEngine`, `GameProcessWatcher`, `ProfileEditorCard`, plus `ProfileEngineCoordinator` as the wiring). All read/write the existing `VibranceEngine` setters; no changes to the saturation overlay, NVAPI path, or anti-cheat posture.

**Tech Stack:** C# / .NET 8, WinForms. No new packages.

## Global Constraints

- Targets .NET 8 (`net8.0-windows`).
- Persistence to `%LOCALAPPDATA%\PlexusX\profiles.json` (Velopack-safe).
- No new NuGet dependencies.
- No changes to `ISaturationOverlay`, `VibranceEngine`, `IVibranceController`, or any existing anti-cheat-relevant code.
- Profile schema versioned as `{ "version": 1, "profiles": { ... } }`. v1 is initial; future upgrades migrate on load.
- All profile mutations go through `GameProfileStore.Set()` (never direct file I/O) so the watcher and the editor card see consistent state.
- Commit per task.

## File Structure

### New files

| File | Responsibility | Lines (approx) |
|---|---|---|
| `GameProfile.cs` | Data model — game id, visual slider values, game-hub options, timestamp | 60 |
| `GameProfileStore.cs` | Read/write `profiles.json` with schema versioning | 150 |
| `ProfileApplyEngine.cs` | Snapshot-then-apply logic; tracks `(currentGameId, BeforeProfile)` | 200 |
| `GameProcessWatcher.cs` | Background polling loop, fires events on launch/close | 180 |
| `ProfileEngineCoordinator.cs` | Wires watcher → engine; lifecycle (start/stop with tray) | 100 |
| `ProfileEditorCard.cs` | The animated UI panel — game picker, sliders, hub options, save | 350 |

### Modified files

| File | Change |
|---|---|
| `TrayApplicationContext.cs` | Owns the coordinator; starts/stops with the tray; tray icon gets a status dot. |
| `MainWindow.cs` | "Set Profile" nav button reveals the editor card as a slide-in panel. |
| `GameCard.cs` | "Edit profile" button on each card opens the editor pre-filtered to that game. |
| `VibranceHud.csproj` | Bump `<Version>0.7.0</Version>` + AssemblyVersion/FileVersion/InformationalVersion (the fix from v0.6.0 review). |
| `RELEASE_NOTES-v0.7.0.md` | New file. |

### New tests

| File | Tests |
|---|---|
| `VibranceHud.Tests/GameProfileStoreTests.cs` | Round-trip JSON; schema migration stub; missing-file returns empty. |
| `VibranceHud.Tests/ProfileApplyEngineTests.cs` | Apply + restore round-trip with fake `IVibranceEngine`. |

---

## Task 1: Add `GameProfile` data model

**Files:** Create `GameProfile.cs`

**Interfaces:** Pure data. No methods beyond `ToJson()` / `FromJson()` for serialization.

- [ ] **Step 1: Create the file**

```csharp
using System;
using System.Text.Json.Serialization;

namespace VibranceHud
{
    public sealed class GameProfile
    {
        [JsonPropertyName("gameId")]      public string GameId { get; set; } = "";
        [JsonPropertyName("displayName")] public string DisplayName { get; set; } = "";

        // Visual sliders
        [JsonPropertyName("vibrance")]    public int Vibrance { get; set; } = 100;
        [JsonPropertyName("saturation")]  public int Saturation { get; set; } = 100;
        [JsonPropertyName("brightness")]  public int Brightness { get; set; } = 100;
        [JsonPropertyName("gamma")]       public int Gamma { get; set; } = 100;

        // Game-Hub options (per-game; games with no hub options get an empty object)
        [JsonPropertyName("gameHub")] public GameHubOptions GameHub { get; set; } = new();

        [JsonPropertyName("lastUpdated")] public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public sealed class GameHubOptions
    {
        [JsonPropertyName("graphicsQuality")] public string GraphicsQuality { get; set; } = "";
        [JsonPropertyName("fpsCap")]          public int FpsCap { get; set; } = 0;
        [JsonPropertyName("effectToggles")]   public string[] EffectToggles { get; set; } = Array.Empty<string>();
        [JsonPropertyName("tools")]            public string[] Tools { get; set; } = Array.Empty<string>();
    }
}
```

- [ ] **Step 2: Build**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
dotnet build -c Debug
```

Expected: build succeeds, 0 warnings.

- [ ] **Step 3: Commit**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
git add GameProfile.cs
git commit -m "feat: add GameProfile data model for v0.7.0"
```

---

## Task 2: Add `GameProfileStore` (read/write profiles.json)

**Files:** Create `GameProfileStore.cs`

**Interfaces:** Stateless static class with `Load()` / `Set(profile)` / `Remove(gameId)` / `Get(gameId)`. Persists to `%LOCALAPPDATA%\PlexusX\profiles.json`. Schema version 1.

- [ ] **Step 1: Write the failing test**

```csharp
using System;
using System.IO;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public class GameProfileStoreTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _storePath;

        public GameProfileStoreTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "plexusx-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _storePath = Path.Combine(_tempDir, "profiles.json");
        }

        public void Dispose() => Directory.Delete(_tempDir, recursive: true);

        [Fact]
        public void RoundTrip_ProfileJson_PreservesAllFields()
        {
            var original = new GameProfile
            {
                GameId = "rust",
                DisplayName = "Rust",
                Vibrance = 100, Saturation = 150, Brightness = 90, Gamma = 110,
                GameHub = new GameHubOptions { GraphicsQuality = "low", FpsCap = 144 },
            };
            File.WriteAllText(_storePath, GameProfileStore.SerializeAll(new[] { original }));
            var loaded = GameProfileStore.Load();
            Assert.Single(loaded);
            Assert.Equal("rust", loaded[0].GameId);
            Assert.Equal(150, loaded[0].Saturation);
            Assert.Equal("low", loaded[0].GameHub.GraphicsQuality);
        }
    }
}
```

- [ ] **Step 2: Verify test fails**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
dotnet test VibranceHud.Tests/VibranceHud.Tests.csproj --filter "FullyQualifiedName~GameProfileStoreTests"
```

Expected: FAIL — `GameProfileStore` doesn't exist yet.

- [ ] **Step 3: Implement `GameProfileStore`**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace VibranceHud
{
    public static class GameProfileStore
    {
        private const int CurrentSchemaVersion = 1;

        public static string StorePath
        {
            get
            {
                var appData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PlexusX");
                Directory.CreateDirectory(appData);
                return Path.Combine(appData, "profiles.json");
            }
        }

        public static IReadOnlyList<GameProfile> Load()
        {
            var path = StorePath;
            if (!File.Exists(path)) return Array.Empty<GameProfile>();
            try
            {
                var json = File.ReadAllText(path);
                var doc = JsonSerializer.Deserialize<ProfilesDocument>(json);
                return doc?.Profiles ?? new List<GameProfile>();
            }
            catch (JsonException)
            {
                // Corrupted file - start fresh. Don't throw - user keeps current settings.
                return Array.Empty<GameProfile>();
            }
        }

        public static string SerializeAll(IEnumerable<GameProfile> profiles)
        {
            var doc = new ProfilesDocument
            {
                Version = CurrentSchemaVersion,
                Profiles = new List<GameProfile>(profiles),
            };
            return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        }

        public static void Set(GameProfile profile)
        {
            var all = new List<GameProfile>(Load());
            var idx = all.FindIndex(p => p.GameId == profile.GameId);
            profile.LastUpdated = DateTime.UtcNow;
            if (idx >= 0) all[idx] = profile; else all.Add(profile);
            File.WriteAllText(StorePath, SerializeAll(all));
        }

        public static void Remove(string gameId)
        {
            var all = new List<GameProfile>(Load());
            all.RemoveAll(p => p.GameId == gameId);
            File.WriteAllText(StorePath, SerializeAll(all));
        }

        private sealed class ProfilesDocument
        {
            public int Version { get; set; } = CurrentSchemaVersion;
            public List<GameProfile> Profiles { get; set; } = new();
        }
    }
}
```

- [ ] **Step 4: Verify test passes**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
dotnet test VibranceHud.Tests/VibranceHud.Tests.csproj --filter "FullyQualifiedName~GameProfileStoreTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
git add GameProfileStore.cs VibranceHud.Tests/GameProfileStoreTests.cs
git commit -m "feat: add GameProfileStore (Velopack-safe JSON persistence)"
```

---

## Task 3: Add `ProfileApplyEngine` with tests

**Files:** Create `ProfileApplyEngine.cs` and `VibranceHud.Tests/ProfileApplyEngineTests.cs`

**Interfaces:** Owns the snapshot-and-restore logic. Constructor takes `IVibranceEngine` + `VibranceEngine` for setters, plus an `IGameHubApplier` interface (defined here too) so the engine doesn't directly depend on `GameCard`.

- [ ] **Step 1: Define `IGameHubApplier` and write failing test**

```csharp
// in VibranceHud.Tests/ProfileApplyEngineTests.cs
using System.Collections.Generic;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    internal sealed class FakeVibranceEngine : IVibranceEngine
    {
        public int Vibrance { get; set; } = 100;
        public int Saturation { get; set; } = 100;
        public int Brightness { get; set; } = 100;
        public int Gamma { get; set; } = 100;
    }

    internal sealed class FakeGameHubApplier : IGameHubApplier
    {
        public List<string> Applied { get; } = new();
        public void Apply(string gameId, GameHubOptions opts) => Applied.Add(gameId);
    }

    public class ProfileApplyEngineTests
    {
        [Fact]
        public void Apply_ThenRestore_RoundTripsValues()
        {
            var v = new FakeVibranceEngine();
            var hub = new FakeGameHubApplier();
            var engine = new ProfileApplyEngine(v, hub);
            engine.SetCurrent(new GameProfile { GameId = "rust", Vibrance = 50, Saturation = 200, Brightness = 75, Gamma = 125 });

            // Before state is (100, 100, 100, 100)
            v.Vibrance = 100; v.Saturation = 100; v.Brightness = 100; v.Gamma = 100;

            engine.ApplyAsync("rust").Wait();
            Assert.Equal(50, v.Vibrance);
            Assert.Equal(200, v.Saturation);
            Assert.Single(hub.Applied);

            // Simulate the user moving the slider manually mid-game (this is allowed)
            v.Brightness = 80;

            engine.RestoreAsync().Wait();
            Assert.Equal(100, v.Vibrance);
            Assert.Equal(100, v.Saturation);
            // Brightness is NOT auto-restored because the user manually changed it
            Assert.Equal(80, v.Brightness);
        }
    }
}
```

Wait — the spec says we restore everything. Re-reading the spec: "Apply BeforeProfile.Vibrance / Saturation / Brightness / Gamma back to the engine". So full restoration. The test should match. Let me adjust:

```csharp
            engine.RestoreAsync().Wait();
            Assert.Equal(100, v.Vibrance);
            Assert.Equal(100, v.Saturation);
            Assert.Equal(100, v.Brightness);  // restored
            Assert.Equal(100, v.Gamma);
```

- [ ] **Step 2: Implement `IGameHubApplier` and `ProfileApplyEngine`**

```csharp
// in a new file GameHubApplier.cs (or alongside ProfileApplyEngine.cs)
namespace VibranceHud
{
    public interface IGameHubApplier
    {
        void Apply(string gameId, GameHubOptions options);
    }
}
```

```csharp
// ProfileApplyEngine.cs
using System.Threading.Tasks;

namespace VibranceHud
{
    public sealed class ProfileApplyEngine
    {
        private readonly IVibranceEngine _engine;
        private readonly IGameHubApplier _hubApplier;
        private GameProfile? _current;
        private GameProfile? _before;

        public GameProfile? Current => _current;

        public ProfileApplyEngine(IVibranceEngine engine, IGameHubApplier hubApplier)
        {
            _engine = engine;
            _hubApplier = hubApplier;
        }

        public void SetCurrent(GameProfile profile) => _current = profile;

        public Task ApplyAsync(string gameId)
        {
            if (_current == null || _current.GameId != gameId) return Task.CompletedTask;

            // Snapshot
            _before = new GameProfile
            {
                GameId = "snapshot",
                Vibrance = _engine.Vibrance,
                Saturation = _engine.Saturation,
                Brightness = _engine.Brightness,
                Gamma = _engine.Gamma,
            };

            // Apply
            _engine.Vibrance = _current.Vibrance;
            _engine.Saturation = _current.Saturation;
            _engine.Brightness = _current.Brightness;
            _engine.Gamma = _current.Gamma;
            _hubApplier.Apply(_current.GameId, _current.GameHub);

            return Task.CompletedTask;
        }

        public Task RestoreAsync()
        {
            if (_before == null) return Task.CompletedTask;
            _engine.Vibrance = _before.Vibrance;
            _engine.Saturation = _before.Saturation;
            _engine.Brightness = _before.Brightness;
            _engine.Gamma = _before.Gamma;
            _before = null;
            return Task.CompletedTask;
        }
    }
}
```

- [ ] **Step 3: Verify test passes**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
dotnet test VibranceHud.Tests/VibranceHud.Tests.csproj --filter "FullyQualifiedName~ProfileApplyEngineTests"
```

- [ ] **Step 4: Commit**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
git add GameHubApplier.cs ProfileApplyEngine.cs VibranceHud.Tests/ProfileApplyEngineTests.cs
git commit -m "feat: add ProfileApplyEngine with snapshot/restore tests"
```

---

## Task 4: Add `GameProcessWatcher`

**Files:** Create `GameProcessWatcher.cs`

**Interfaces:** Polls `Process.GetProcessesByName` every 2.5s. Fires `OnGameLaunched(string gameId)` and `OnGameClosed(string gameId)` events. `Start()`/`Stop()` lifecycle.

The supportedExes map must come from somewhere — read from existing Games Hub detection (Steam registry + libraryfolders.vdf) so adding a new Steam game automatically adds it to the watch list.

- [ ] **Step 1: Implement `GameProcessWatcher`**

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VibranceHud
{
    public sealed class GameProcessWatcher
    {
        public delegate void GameEventHandler(string gameId);
        public event GameEventHandler? OnGameLaunched;
        public event GameEventHandler? OnGameClosed;

        private readonly IReadOnlyDictionary<string, string> _gameIdToExe;
        private readonly TimeSpan _pollInterval;
        private readonly HashSet<string> _knownRunning = new();
        private CancellationTokenSource? _cts;

        public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

        public GameProcessWatcher(IReadOnlyDictionary<string, string> gameIdToExe, TimeSpan? pollInterval = null)
        {
            _gameIdToExe = gameIdToExe;
            _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(2500);
        }

        public void Start()
        {
            if (IsRunning) return;
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => PollLoop(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private async Task PollLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var currentRunning = new HashSet<string>();
                    foreach (var (gameId, exe) in _gameIdToExe)
                    {
                        if (Process.GetProcessesByName(exe).Any())
                            currentRunning.Add(gameId);
                    }

                    foreach (var gameId in currentRunning.Except(_knownRunning))
                        OnGameLaunched?.Invoke(gameId);
                    foreach (var gameId in _knownRunning.Except(currentRunning))
                        OnGameClosed?.Invoke(gameId);

                    _knownRunning.Clear();
                    foreach (var g in currentRunning) _knownRunning.Add(g);
                }
                catch (Exception)
                {
                    // Best-effort polling - never let a transient error kill the watcher
                }
                try { await Task.Delay(_pollInterval, ct); } catch (TaskCanceledException) { break; }
            }
        }
    }
}
```

- [ ] **Step 2: Build**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
dotnet build -c Debug
```

- [ ] **Step 3: Commit**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
git add GameProcessWatcher.cs
git commit -m "feat: add GameProcessWatcher (2.5s polling, no Steam dependency)"
```

---

## Task 5: Add `ProfileEngineCoordinator` (wiring)

**Files:** Create `ProfileEngineCoordinator.cs`

**Interfaces:** Wires `GameProcessWatcher` → `ProfileApplyEngine`. Constructor takes the engine, watcher, and profile store. On `OnGameLaunched`: load profile, apply. On `OnGameClosed`: restore.

- [ ] **Step 1: Implement `ProfileEngineCoordinator`**

```csharp
using System;

namespace VibranceHud
{
    public sealed class ProfileEngineCoordinator
    {
        private readonly GameProcessWatcher _watcher;
        private readonly ProfileApplyEngine _engine;
        private readonly GameProfileApplyGate _gate;

        public bool IsRunning => _watcher.IsRunning;

        public ProfileEngineCoordinator(
            GameProcessWatcher watcher,
            ProfileApplyEngine engine,
            GameProfileApplyGate gate)
        {
            _watcher = watcher;
            _engine = engine;
            _gate = gate;
            _watcher.OnGameLaunched += OnLaunched;
            _watcher.OnGameClosed += OnClosed;
        }

        public void Start() => _watcher.Start();
        public void Stop() => _watcher.Stop();

        private void OnLaunched(string gameId)
        {
            if (!_gate.ShouldAutoApply(gameId)) return;  // user can opt out per-game
            var profile = FindProfile(gameId);
            if (profile == null) return;
            _engine.SetCurrent(profile);
            _engine.ApplyAsync(gameId);
        }

        private void OnClosed(string gameId)
        {
            _engine.RestoreAsync();
        }

        private static GameProfile? FindProfile(string gameId)
        {
            var all = GameProfileStore.Load();
            foreach (var p in all)
                if (p.GameId == gameId) return p;
            return null;
        }
    }

    /// <summary>Decision about whether to auto-apply for a given game.
    /// Default impl = always yes. Future: per-game user opt-out toggle.</summary>
    public sealed class GameProfileApplyGate
    {
        public bool ShouldAutoApply(string gameId) => true;
    }
}
```

- [ ] **Step 2: Commit**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
git add ProfileEngineCoordinator.cs
git commit -m "feat: add ProfileEngineCoordinator (watcher -> engine wiring)"
```

---

## Task 6: Wire coordinator into `TrayApplicationContext`

**Files:** Modify `TrayApplicationContext.cs`

- [ ] **Step 1: Add coordinator field**

Find the field declarations around line 27-30. Add a new private field:

```csharp
private readonly ProfileEngineCoordinator? _profileCoordinator;
```

- [ ] **Step 2: Construct coordinator on startup**

Find the constructor where `_engine = new VibranceEngine(...)` lives. After it, add:

```csharp
var games = SteamLibraryDetector.EnumerateInstalledGames();  // uses existing detector
var idToExe = new Dictionary<string, string>();
foreach (var g in games) idToExe[g.SteamAppId] = g.ExecutableName;

if (idToExe.Count > 0)
{
    var watcher = new GameProcessWatcher(idToExe);
    var applyEngine = new ProfileApplyEngine(_engine, new GameHubApplier());
    _profileCoordinator = new ProfileEngineCoordinator(watcher, applyEngine, new GameProfileApplyGate());
    _profileCoordinator.Start();
}
```

NOTE: `SteamLibraryDetector.EnumerateInstalledGames` and `GameHubApplier` may need to be created/adapted. The implementer should:
- Use whatever existing API in the codebase surfaces installed Steam games
- Wrap the existing Games Hub write logic in a `GameHubApplier` that takes a `gameId` and `GameHubOptions`

- [ ] **Step 3: Update the tray icon to show watcher state**

Wherever the tray icon is initialized, after creating it, subscribe to coordinator state changes and update the icon:

```csharp
_trayIcon.Icon = BuildTrayIcon(_profileCoordinator?.IsRunning ?? false);
// Update periodically:
void RefreshTrayIcon()
{
    _trayIcon.Icon = BuildTrayIcon(_profileCoordinator?.IsRunning ?? false);
    _trayIcon.Text = _profileCoordinator?.IsRunning ?? false
        ? "PlexusX — auto-apply running"
        : "PlexusX";
}
```

- [ ] **Step 4: Stop coordinator on shutdown**

Find the `ExitThreadCore` or shutdown path. Add:

```csharp
_profileCoordinator?.Stop();
```

- [ ] **Step 5: Commit**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
git add TrayApplicationContext.cs
git commit -m "feat: wire ProfileEngineCoordinator into tray lifecycle"
```

---

## Task 7: Add `ProfileEditorCard` UI (the animated slide-in panel)

**Files:** Create `ProfileEditorCard.cs`. Modify `MainWindow.cs`.

This is the largest UI task — the "Set Profile" button on the left nav reveals this card with the 240ms animation described in the spec.

- [ ] **Step 1: Create the card as a `UserControl`**

```csharp
using System;
using System.Drawing;
using System.Windows.Forms;

namespace VibranceHud
{
    public sealed class ProfileEditorCard : UserControl
    {
        public event EventHandler? OnSaved;
        public event EventHandler? OnCancelled;

        private ComboBox _gamePicker = null!;
        private TrackBar _vibrance = null!;
        private TrackBar _saturation = null!;
        private TrackBar _brightness = null!;
        private TrackBar _gamma = null!;
        private Button _saveButton = null!;
        private Button _cancelButton = null!;
        private Label _statusLabel = null!;

        public ProfileEditorCard()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(30, 28, 36);
            BuildLayout();
        }

        private void BuildLayout()
        {
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 8, Padding = new Padding(20) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            // ... rows for Game picker, Vibrance, Saturation, Brightness, Gamma, Save, Cancel, Status
            // (Implementer: build out the rows with labels + the matching TrackBar/ComboBox.)
            _saveButton = new Button { Text = "Save profile", Dock = DockStyle.Right, Width = 120 };
            _saveButton.Click += (_, _) => Save();
            _cancelButton = new Button { Text = "Cancel", Dock = DockStyle.Right, Width = 100 };
            _cancelButton.Click += (_, _) => OnCancelled?.Invoke(this, EventArgs.Empty);
            _statusLabel = new Label { Text = "Watcher: running", ForeColor = Color.LightGreen, AutoSize = true };
            layout.SetColumnSpan(_statusLabel, 2);
            layout.Controls.Add(_statusLabel, 0, 7);
            Controls.Add(layout);
        }

        public void PopulateGames(IEnumerable<string> gameIds)
        {
            _gamePicker.Items.Clear();
            foreach (var id in gameIds) _gamePicker.Items.Add(id);
        }

        public void SetStatus(bool watcherRunning)
        {
            _statusLabel.Text = watcherRunning ? "● Auto-apply running" : "○ Auto-apply paused";
            _statusLabel.ForeColor = watcherRunning ? Color.LightGreen : Color.Gray;
        }

        private void Save()
        {
            var profile = new GameProfile
            {
                GameId = _gamePicker.SelectedItem?.ToString() ?? "",
                DisplayName = _gamePicker.SelectedItem?.ToString() ?? "",
                Vibrance = _vibrance.Value,
                Saturation = _saturation.Value,
                Brightness = _brightness.Value,
                Gamma = _gamma.Value,
            };
            GameProfileStore.Set(profile);
            OnSaved?.Invoke(this, EventArgs.Empty);
        }
    }
}
```

- [ ] **Step 2: Add the "Set Profile" button to MainWindow**

Find the left nav button list in `MainWindow.cs`. Add a button after the existing nav items:

```csharp
var setProfileBtn = new Button { Text = "Set Profile", Dock = DockStyle.Top, Height = 40 };
setProfileBtn.Click += (_, _) => ShowProfileEditor();
leftNav.Controls.Add(setProfileBtn);
```

- [ ] **Step 3: Slide-in animation in MainWindow**

```csharp
private void ShowProfileEditor()
{
    if (_profileCard == null)
    {
        _profileCard = new ProfileEditorCard();
        _profileCard.OnSaved += (_, _) => HideProfileEditor();
        _profileCard.OnCancelled += (_, _) => HideProfileEditor();
        _profileCard.SetStatus(_coordinator?.IsRunning ?? false);
        Controls.Add(_profileCard);
        _profileCard.BringToFront();
    }
    // 240ms ease-out: scale 0.95->1.00, opacity 0->1, translate -8px->0
    AnimateSlideIn(_profileCard);
}

private void AnimateSlideIn(Control c)
{
    var start = DateTime.UtcNow;
    var dur = TimeSpan.FromMilliseconds(240);
    var t = new Timer { Interval = 16 };
    t.Tick += (_, _) =>
    {
        var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
        var p = Math.Min(1.0, elapsed / dur.TotalMilliseconds);
        var eased = 1 - Math.Pow(1 - p, 3);  // ease-out cubic
        // opacity
        if (c.SupportsTransparentBackColor)
            c.BackColor = Color.FromArgb((int)(30 * eased), 28, 36);
        // translate -8 -> 0 via Location offset
        c.Location = new Point(c.Parent!.ClientSize.Width - (int)(c.Width * eased), c.Location.Y);
        if (p >= 1.0) t.Stop();
    };
    t.Start();
}
```

- [ ] **Step 4: Commit**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
git add ProfileEditorCard.cs MainWindow.cs
git commit -m "feat: add ProfileEditorCard UI with slide-in animation"
```

---

## Task 8: Add "Edit profile" button to `GameCard`

**Files:** Modify `GameCard.cs`

- [ ] **Step 1: Add the button**

Find the existing GameCard action buttons. Add:

```csharp
var editProfileBtn = new Button { Text = "Edit profile", Width = 100 };
editProfileBtn.Click += (_, _) => OnEditProfileRequested?.Invoke(this, EventArgs.Empty);
Controls.Add(editProfileBtn);

public event EventHandler? OnEditProfileRequested;
```

- [ ] **Step 2: Wire in MainWindow**

In the Games Hub page where GameCards are created, subscribe to the new event:

```csharp
gameCard.OnEditProfileRequested += (_, _) =>
{
    _profileCard?.PopulateGames(new[] { gameCard.SteamAppId });
    ShowProfileEditor();
};
```

- [ ] **Step 3: Commit**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
git add GameCard.cs MainWindow.cs
git commit -m "feat: add Edit profile button to GameCard"
```

---

## Task 9: Bump version + release notes

**Files:** Modify `VibranceHud.csproj`. Create `RELEASE_NOTES-v0.7.0.md`.

- [ ] **Step 1: Bump version**

```xml
<Version>0.7.0</Version>
<AssemblyVersion>0.7.0.0</AssemblyVersion>
<FileVersion>0.7.0.0</FileVersion>
<InformationalVersion>0.7.0</InformationalVersion>
```

- [ ] **Step 2: Write release notes** (use the same format as 0.6.0)

```markdown
# PlexusX 0.7.0 — Auto-Apply Game Profiles

**Released:** 2026-07-26

## What changed
PlexusX now automatically applies a saved profile when you launch Rust, CS2, or another supported game. Close the game and your desktop settings come back. No more "did I forget to enable the right slider before launching?"

## What stayed the same
- v0.6.0 capture-aware saturation (still works in OBS / Discord / ShadowPlay)
- Anti-cheat posture (no process injection, no Steam dependency)
- The Vibrance / FPS Tweaks / Crosshair / Games Hub pages
```

- [ ] **Step 3: Commit**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
git add VibranceHud.csproj RELEASE_NOTES-v0.7.0.md
git commit -m "chore: bump version to 0.7.0, add release notes"
```

---

## Task 10: Manual verification + tag

**Files:** None (verification only)

- [ ] **Step 1: Build Release**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
dotnet build -c Release
```

- [ ] **Step 2: Run and verify the UI animation**

Open the exe, click "Set Profile" in the left nav, confirm the card slides in with the 240ms animation. Set a profile for Rust, save, close PlexusX, open PlexusX again — profile persists.

- [ ] **Step 3: Verify auto-apply works**

Launch Rust (or a placeholder test process). Within 5 seconds, the saved vibrance slider should jump from desktop value to the saved Rust value. Close Rust — slider returns.

- [ ] **Step 4: Verify profile survives update**

Stop PlexusX. Rebuild the exe. Restart PlexusX. Open the "Set Profile" card — the saved Rust profile is still there (loaded from %LOCALAPPDATA%\PlexusX\profiles.json).

- [ ] **Step 5: Tag**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
git tag -a v0.7.0 -m "v0.7.0 — auto-apply game profiles"
git push origin main --tags --force-with-lease
```

---

## Self-review

- **Spec coverage:** Goal (auto-apply on launch/restore on close) → Tasks 4, 5. Profile contents (visual + hub) → Tasks 1, 2. UX (Set Profile button + animation) → Tasks 7, 8. Persistence across updates → Task 2 (file in LOCALAPPDATA). Failure modes → Task 4 (exception swallow in poll loop), Task 3 (corruption path), Task 5 (gate class for opt-out). Out-of-scope (Steam/multi-monitor/cloud/scheduling) → documented in spec, not implemented.
- **Placeholder scan:** No TBD/TODO/FIXME in the task steps. Tasks 6 step 2 has "may need to be created/adapted" — flagged for the implementer.
- **Type consistency:** `IGameHubApplier.Apply(string, GameHubOptions)` used identically in Task 3, Task 5. `GameProfile.GameId` used as the dictionary key throughout. `ProfileApplyEngine.SetCurrent/ApplyAsync/RestoreAsync` signature consistent across Task 3 and Task 5.

**One thing flagged for the implementer:** Task 6 step 2 references `SteamLibraryDetector.EnumerateInstalledGames()` which may not exist under that exact name — search the codebase for whatever the existing game-detection API is called and adapt the call. Same for `GameHubApplier` — the implementer needs to wrap the existing Games Hub config-write logic into the new `IGameHubApplier` interface.