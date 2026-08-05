using System;
using System.Collections.Generic;
using System.IO;
using VibranceHud;
using VibranceHud.Games;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// The one selection the whole shell reads. Everything here is about it degrading to
    /// Desktop rather than pointing pages at a game that isn't there.
    /// </summary>
    public sealed class GameSelectionTests : IDisposable
    {
        private readonly string _dir = Path.Combine(
            Path.GetTempPath(), "PlexusXSel_" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private static IReadOnlyList<DetectedGame> Installed(params SupportedGame[] games)
        {
            var list = new List<DetectedGame>();
            foreach (var g in games) list.Add(new DetectedGame(g, @"C:\games\" + g.Id));
            return list;
        }

        private GameSelection Build(string savedId, params SupportedGame[] installed)
        {
            var settings = new AppSettings { CurrentGameId = savedId };
            return new GameSelection(settings, new SettingsStore(_dir), () => Installed(installed));
        }

        [Fact]
        public void A_saved_game_that_is_installed_comes_back_selected()
        {
            var sel = Build("rust", SupportedGames.Rust, SupportedGames.Cs2);

            Assert.Equal("rust", sel.CurrentId);
            Assert.Equal(SupportedGames.Rust, sel.Current);
            Assert.NotNull(sel.Detected);
        }

        [Fact]
        public void No_saved_game_means_desktop()
        {
            var sel = Build("", SupportedGames.Rust);

            Assert.Null(sel.CurrentId);
            Assert.Null(sel.Current);
            Assert.Null(sel.Detected);
        }

        [Fact]
        public void A_game_that_has_since_been_uninstalled_falls_back_to_desktop()
        {
            // The reason this matters: otherwise the Game tab opens pointed at a game whose
            // config files are gone, and every page on it fails against a missing folder.
            var sel = Build("rust", SupportedGames.Cs2);

            Assert.Null(sel.CurrentId);
        }

        [Fact]
        public void An_id_that_isnt_in_the_catalogue_falls_back_to_desktop()
        {
            // A downgrade, or somebody editing settings.json by hand.
            var sel = Build("minecraft", SupportedGames.Rust);

            Assert.Null(sel.CurrentId);
        }

        [Fact]
        public void Selecting_raises_changed_once_and_persists()
        {
            var settings = new AppSettings();
            var store = new SettingsStore(_dir);
            var sel = new GameSelection(settings, store,
                () => Installed(SupportedGames.Rust, SupportedGames.Cs2));

            int raised = 0;
            sel.Changed += (_, _) => raised++;

            sel.Select("cs2");

            Assert.Equal(1, raised);
            Assert.Equal("cs2", sel.CurrentId);
            Assert.Equal("cs2", settings.CurrentGameId);
        }

        [Fact]
        public void Re_selecting_the_same_game_changes_nothing()
        {
            // Pages rebuild on Changed. Rebuilding to show what is already on screen is a
            // visible flicker for no reason.
            var sel = Build("rust", SupportedGames.Rust);
            int raised = 0;
            sel.Changed += (_, _) => raised++;

            sel.Select("rust");

            Assert.Equal(0, raised);
        }

        [Fact]
        public void Selecting_an_uninstalled_game_lands_on_desktop_rather_than_pretending()
        {
            var sel = Build("rust", SupportedGames.Rust);

            sel.Select("apex");   // not installed in this fixture

            Assert.Null(sel.CurrentId);
        }

        [Fact]
        public void Selecting_desktop_from_a_game_raises_and_clears()
        {
            var sel = Build("rust", SupportedGames.Rust);
            int raised = 0;
            sel.Changed += (_, _) => raised++;

            sel.Select(null);

            Assert.Equal(1, raised);
            Assert.Null(sel.CurrentId);
        }

        [Fact]
        public void Installed_lists_only_what_was_detected()
        {
            var sel = Build("", SupportedGames.Rust, SupportedGames.Fortnite);

            Assert.Equal(2, sel.Installed.Count);
            Assert.False(sel.NothingInstalled);
        }

        [Fact]
        public void A_machine_with_no_supported_games_reports_it_rather_than_throwing()
        {
            var sel = Build("rust");

            Assert.True(sel.NothingInstalled);
            Assert.Null(sel.CurrentId);
        }

        [Fact]
        public void Detection_blowing_up_leaves_the_app_usable()
        {
            // GameLibrary already swallows its own failures, but the shell must not depend on
            // that - a throw here would take the whole window down at construction.
            var settings = new AppSettings { CurrentGameId = "rust" };
            var sel = new GameSelection(settings, new SettingsStore(_dir),
                () => throw new InvalidOperationException("registry exploded"));

            Assert.True(sel.NothingInstalled);
            Assert.Null(sel.CurrentId);
        }

        [Fact]
        public void Refresh_drops_a_selection_whose_game_vanished()
        {
            var settings = new AppSettings { CurrentGameId = "rust" };
            bool installed = true;
            var sel = new GameSelection(settings, new SettingsStore(_dir),
                () => installed ? Installed(SupportedGames.Rust) : Installed());

            Assert.Equal("rust", sel.CurrentId);

            installed = false;
            int raised = 0;
            sel.Changed += (_, _) => raised++;
            sel.Refresh();

            Assert.Null(sel.CurrentId);
            Assert.Equal(1, raised);
        }
    }
}
