using System.Collections.Generic;
using System.Linq;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Per-game launch resolution. The rules themselves are pure, so everything that matters
    /// about them is testable without a monitor attached.
    /// </summary>
    public sealed class MonitorRuleTests
    {
        [Fact]
        public void A_game_with_no_rule_leaves_the_resolution_alone()
        {
            Assert.Null(MonitorRules.For(new List<MonitorRule>(), "rust"));
        }

        [Fact]
        public void Setting_a_rule_then_reading_it_back_gives_the_same_mode()
        {
            var rules = MonitorRules.Set(new List<MonitorRule>(), "rust", 1280, 1024);

            var rule = MonitorRules.For(rules, "rust");

            Assert.NotNull(rule);
            Assert.Equal(1280, rule!.Width);
            Assert.Equal(1024, rule.Height);
        }

        [Fact]
        public void Setting_a_rule_twice_replaces_rather_than_stacking()
        {
            var rules = MonitorRules.Set(new List<MonitorRule>(), "rust", 1280, 1024);
            rules = MonitorRules.Set(rules, "rust", 1600, 1080);

            Assert.Single(rules);
            Assert.Equal(1600, MonitorRules.For(rules, "rust")!.Width);
        }

        [Fact]
        public void One_games_rule_does_not_disturb_another()
        {
            var rules = MonitorRules.Set(new List<MonitorRule>(), "rust", 1280, 1024);
            rules = MonitorRules.Set(rules, "cs2", 1600, 1080);

            Assert.Equal(1280, MonitorRules.For(rules, "rust")!.Width);
            Assert.Equal(1600, MonitorRules.For(rules, "cs2")!.Width);
        }

        [Fact]
        public void Clearing_a_rule_returns_that_game_to_leaving_it_alone()
        {
            var rules = MonitorRules.Set(new List<MonitorRule>(), "rust", 1280, 1024);
            rules = MonitorRules.Clear(rules, "rust");

            Assert.Null(MonitorRules.For(rules, "rust"));
        }

        [Fact]
        public void Clearing_one_game_leaves_the_others_intact()
        {
            var rules = MonitorRules.Set(new List<MonitorRule>(), "rust", 1280, 1024);
            rules = MonitorRules.Set(rules, "cs2", 1600, 1080);

            rules = MonitorRules.Clear(rules, "rust");

            Assert.Null(MonitorRules.For(rules, "rust"));
            Assert.NotNull(MonitorRules.For(rules, "cs2"));
        }

        [Fact]
        public void A_zero_sized_rule_counts_as_no_rule()
        {
            // "Don't change" is stored as 0x0 rather than by deleting the entry, so this is
            // the shape that comes back from the UI and it must not read as a real mode.
            var rules = new List<MonitorRule> { new() { GameId = "rust", Width = 0, Height = 0 } };

            Assert.Null(MonitorRules.For(rules, "rust"));
        }

        [Fact]
        public void Game_ids_match_regardless_of_case()
        {
            var rules = MonitorRules.Set(new List<MonitorRule>(), "Rust", 1280, 1024);

            Assert.NotNull(MonitorRules.For(rules, "rust"));
        }

        [Fact]
        public void A_null_or_empty_game_never_matches_a_rule()
        {
            var rules = MonitorRules.Set(new List<MonitorRule>(), "rust", 1280, 1024);

            Assert.Null(MonitorRules.For(rules, null));
            Assert.Null(MonitorRules.For(rules, ""));
        }

        [Fact]
        public void Every_catalogue_game_has_a_process_name_to_watch_for()
        {
            // The launch rule restores the desktop when the game's process exits. A game with
            // no process name would switch the resolution and never switch it back.
            foreach (var game in Games.SupportedGames.All)
                Assert.False(string.IsNullOrWhiteSpace(game.ProcessName));

            Assert.Equal(Games.SupportedGames.All.Count,
                Games.SupportedGames.ProcessNames.Count);
        }
    }
}
