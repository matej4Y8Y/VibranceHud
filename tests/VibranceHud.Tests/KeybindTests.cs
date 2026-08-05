using System.Collections.Generic;
using System.Linq;
using VibranceHud.Keybinds;
using Xunit;

namespace VibranceHud.Tests
{
    public sealed class KeybindTests
    {
        // ---- the set --------------------------------------------------------------------

        [Fact]
        public void A_key_with_nothing_on_it_reports_nothing()
        {
            Assert.Null(KeybindSet.OnKey(new List<Keybind>(), "rust", "f1"));
        }

        [Fact]
        public void Assigning_then_reading_back_gives_the_same_command()
        {
            var binds = KeybindSet.Assign(new List<Keybind>(), "rust", "f1", "rust.craft.bandage");

            Assert.Equal("rust.craft.bandage", KeybindSet.OnKey(binds, "rust", "f1")!.CommandId);
        }

        [Fact]
        public void One_command_per_key_assigning_replaces()
        {
            // Stacking commands on a key is how you get a config that fires three things at
            // once with no way to see why.
            var binds = KeybindSet.Assign(new List<Keybind>(), "rust", "f1", "rust.craft.bandage");
            binds = KeybindSet.Assign(binds, "rust", "f1", "rust.combatlog");

            Assert.Single(KeybindSet.For(binds, "rust"));
            Assert.Equal("rust.combatlog", KeybindSet.OnKey(binds, "rust", "f1")!.CommandId);
        }

        [Fact]
        public void The_same_key_in_two_games_is_two_different_binds()
        {
            var binds = KeybindSet.Assign(new List<Keybind>(), "rust", "f1", "rust.combatlog");
            binds = KeybindSet.Assign(binds, "cs2", "f1", "cs2.buy.ak");

            Assert.Equal("rust.combatlog", KeybindSet.OnKey(binds, "rust", "f1")!.CommandId);
            Assert.Equal("cs2.buy.ak", KeybindSet.OnKey(binds, "cs2", "f1")!.CommandId);
        }

        [Fact]
        public void Clearing_a_key_leaves_the_rest_of_the_game_alone()
        {
            var binds = KeybindSet.Assign(new List<Keybind>(), "rust", "f1", "rust.combatlog");
            binds = KeybindSet.Assign(binds, "rust", "f2", "rust.kill");

            binds = KeybindSet.Clear(binds, "rust", "f1");

            Assert.Null(KeybindSet.OnKey(binds, "rust", "f1"));
            Assert.NotNull(KeybindSet.OnKey(binds, "rust", "f2"));
        }

        [Fact]
        public void Clearing_one_game_never_touches_another()
        {
            var binds = KeybindSet.Assign(new List<Keybind>(), "rust", "f1", "rust.combatlog");
            binds = KeybindSet.Assign(binds, "cs2", "f1", "cs2.buy.ak");

            binds = KeybindSet.ClearGame(binds, "rust");

            Assert.Empty(KeybindSet.For(binds, "rust"));
            Assert.Single(KeybindSet.For(binds, "cs2"));
        }

        // ---- the catalogue --------------------------------------------------------------

        [Fact]
        public void Command_ids_are_unique_within_a_game()
        {
            // Ids are what saved binds point at. A duplicate would make a bind ambiguous.
            foreach (var gameId in new[] { "rust", "cs2" })
            {
                var ids = GameCommands.For(gameId).Select(c => c.Id).ToList();
                Assert.Equal(ids.Count, ids.Distinct().Count());
            }
        }

        [Fact]
        public void Every_command_has_a_label_a_body_and_an_explanation()
        {
            foreach (var gameId in new[] { "rust", "cs2" })
                foreach (var c in GameCommands.For(gameId))
                {
                    Assert.False(string.IsNullOrWhiteSpace(c.Label));
                    Assert.False(string.IsNullOrWhiteSpace(c.Command));
                    Assert.False(string.IsNullOrWhiteSpace(c.Description));
                }
        }

        [Fact]
        public void A_game_we_have_not_catalogued_offers_nothing_rather_than_guesses()
        {
            // Inventing plausible commands would write something broken into a real config.
            Assert.Empty(GameCommands.For("apex"));
            Assert.Empty(GameCommands.For(null));
        }

        [Fact]
        public void Every_craft_bind_carries_a_numeric_item_id_and_a_quantity()
        {
            // A malformed craft.add is written straight into a real config and then silently
            // does nothing in game, which is indistinguishable from the app being broken.
            foreach (var c in GameCommands.For("rust").Where(c => c.Command.StartsWith("craft.add")))
            {
                var parts = c.Command.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
                Assert.Equal(3, parts.Length);
                Assert.True(int.TryParse(parts[1], out _), $"{c.Id} has a non-numeric item id");
                Assert.True(int.TryParse(parts[2], out var qty) && qty > 0,
                    $"{c.Id} has no usable quantity");
            }
        }

        [Fact]
        public void Rust_has_a_real_catalogue_not_a_token_one()
        {
            // Rust is the flagship game; a handful of commands would make the page look
            // unfinished next to the keyboard it sits beside.
            Assert.True(GameCommands.For("rust").Count >= 25);

            // And every category is represented, so the palette never shows an empty group.
            foreach (CommandCategory category in System.Enum.GetValues<CommandCategory>())
                Assert.Contains(GameCommands.For("rust"), c => c.Category == category);
        }

        [Fact]
        public void The_catalogue_contains_no_exploit_commands()
        {
            // The line, asserted rather than trusted to review. gc.collect spam is the Rust
            // levitation glitch; noclip and godmode are server-side cheats. Shipping any of
            // them would put PlexusX on cheat lists and get its users banned.
            string[] banned = { "gc.collect", "noclip", "god", "cheat" };

            foreach (var gameId in new[] { "rust", "cs2" })
                foreach (var c in GameCommands.For(gameId))
                    foreach (var bad in banned)
                        Assert.DoesNotContain(bad, c.Command.ToLowerInvariant());
        }

        // ---- the writer -----------------------------------------------------------------

        [Fact]
        public void Rust_binds_come_out_in_rust_syntax()
        {
            var binds = KeybindSet.Assign(new List<Keybind>(), "rust", "f1", "rust.combatlog");

            var text = KeybindWriter.Build(binds, "rust");

            Assert.Contains("bind f1 \"consoletoggle;combatlog\"", text);
        }

        [Fact]
        public void Cs2_binds_come_out_quoted_the_way_cs2_wants()
        {
            var binds = KeybindSet.Assign(new List<Keybind>(), "cs2", "f1", "cs2.buy.ak");

            var text = KeybindWriter.Build(binds, "cs2");

            Assert.Contains("bind \"f1\" \"buy ak47\"", text);
        }

        [Fact]
        public void A_bind_pointing_at_a_command_that_no_longer_exists_is_skipped_not_written_broken()
        {
            var binds = KeybindSet.Assign(new List<Keybind>(), "rust", "f1", "rust.removed.command");

            var text = KeybindWriter.Build(binds, "rust");

            Assert.DoesNotContain("f1", text);
        }

        [Fact]
        public void Merging_into_an_empty_config_just_writes_the_block()
        {
            var block = KeybindWriter.Build(
                KeybindSet.Assign(new List<Keybind>(), "rust", "f1", "rust.kill"), "rust");

            var merged = KeybindWriter.Merge("", block);

            Assert.Contains("bind f1", merged);
        }

        [Fact]
        public void Merging_preserves_everything_the_user_wrote_themselves()
        {
            // The whole reason for the markers. People have their own binds and settings in
            // these files and eating them would be far worse than this feature is good.
            const string mine = "graphics.fov 90\r\nbind mouse4 +duck\r\n";
            var block = KeybindWriter.Build(
                KeybindSet.Assign(new List<Keybind>(), "rust", "f1", "rust.kill"), "rust");

            var merged = KeybindWriter.Merge(mine, block);

            Assert.Contains("graphics.fov 90", merged);
            Assert.Contains("bind mouse4 +duck", merged);
            Assert.Contains("bind f1", merged);
        }

        [Fact]
        public void Writing_twice_replaces_our_block_rather_than_stacking_it()
        {
            const string mine = "graphics.fov 90\r\n";
            var first = KeybindWriter.Build(
                KeybindSet.Assign(new List<Keybind>(), "rust", "f1", "rust.kill"), "rust");
            var second = KeybindWriter.Build(
                KeybindSet.Assign(new List<Keybind>(), "rust", "f2", "rust.combatlog"), "rust");

            var merged = KeybindWriter.Merge(KeybindWriter.Merge(mine, first), second);

            Assert.Single(AllIndexesOf(merged, KeybindWriter.BeginMarker));
            Assert.Contains("graphics.fov 90", merged);
            Assert.Contains("bind f2", merged);
            Assert.DoesNotContain("bind f1", merged);
        }

        private static List<int> AllIndexesOf(string haystack, string needle)
        {
            var hits = new List<int>();
            for (int i = haystack.IndexOf(needle, System.StringComparison.Ordinal); i >= 0;
                 i = haystack.IndexOf(needle, i + 1, System.StringComparison.Ordinal))
                hits.Add(i);
            return hits;
        }
    }
}
