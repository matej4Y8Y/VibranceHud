using VibranceHud.Keybinds;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// The keys each game already uses.
    ///
    /// The point is to stop somebody binding a PlexusX command over their own reload or
    /// sprint. The board otherwise shows sixty free-looking keys, most of which are not free.
    /// </summary>
    public sealed class GameDefaultBindsTests
    {
        [Theory]
        [InlineData("rust")]
        [InlineData("cs2")]
        public void MovementAndCombatKeysAreKnownForSupportedGames(string game)
        {
            var defaults = GameDefaultBinds.For(game);

            foreach (var key in new[] { "w", "a", "s", "d", "space", "r", "e", "mouse1" })
                Assert.True(defaults.ContainsKey(key), $"{game} should know about '{key}'");
        }

        /// <summary>Rust's Tab opens the inventory; CS2's shows the scoreboard. The
        /// game-specific list has to win over the shared one, or every game gets the same
        /// wrong label.</summary>
        [Fact]
        public void GameSpecificMeaningsWinOverTheSharedOnes()
        {
            Assert.Equal("Inventory", GameDefaultBinds.For("rust")["tab"]);
            Assert.Equal("Scoreboard", GameDefaultBinds.For("cs2")["tab"]);
        }

        [Fact]
        public void EachGameKeepsItsOwnNumberRowMeanings()
        {
            Assert.Equal("Hotbar 1", GameDefaultBinds.For("rust")["1"]);
            Assert.Equal("Primary", GameDefaultBinds.For("cs2")["1"]);
        }

        /// <summary>
        /// A game we have not catalogued gets nothing at all, rather than the common shooter
        /// keys. Showing a guess as fact is the mistake this whole area is here to avoid, and
        /// a wrong warning trains people to ignore the right ones.
        /// </summary>
        [Theory]
        [InlineData("apex")]
        [InlineData("fortnite")]
        [InlineData("something-else")]
        [InlineData("")]
        [InlineData(null)]
        public void AnUncataloguedGameClaimsNothing(string? game)
        {
            Assert.Empty(GameDefaultBinds.For(game));
            Assert.False(GameDefaultBinds.Knows(game));
        }

        [Theory]
        [InlineData("rust")]
        [InlineData("cs2")]
        public void KnowsIsTrueOnlyForCataloguedGames(string game)
        {
            Assert.True(GameDefaultBinds.Knows(game));
        }

        [Fact]
        public void LookupIsCaseInsensitiveOnBothGameAndKey()
        {
            Assert.True(GameDefaultBinds.Knows("RUST"));
            Assert.True(GameDefaultBinds.For("Rust").ContainsKey("W"));
        }

        /// <summary>
        /// Every key named here has to exist on the drawn keyboard, or the warning is
        /// invisible: the board can only tint a key it actually draws.
        /// </summary>
        [Theory]
        [InlineData("rust")]
        [InlineData("cs2")]
        public void EveryDefaultKeyExistsOnTheDrawnKeyboard(string game)
        {
            var onBoard = VibranceHud.KeyboardView.AllKeyIds();

            foreach (var key in GameDefaultBinds.For(game).Keys)
                Assert.True(onBoard.Contains(key),
                    $"'{key}' is listed for {game} but the keyboard doesn't draw it");
        }
    }
}
