using VibranceHud.Games;
using Xunit;

namespace VibranceHud.Tests
{
    public class GamePageRouterTests
    {
        [Theory]
        [InlineData("rust", GamePageKind.Rust)]
        [InlineData("cs2", GamePageKind.Cs2)]
        [InlineData("apex", GamePageKind.Apex)]
        [InlineData("fortnite", GamePageKind.Fortnite)]
        public void Resolve_RoutesKnownIds_ToTheirOwnPage(string id, GamePageKind expected)
        {
            Assert.Equal(expected, GamePageRouter.Resolve(id));
        }

        [Theory]
        [InlineData("")]
        [InlineData("valorant")]
        [InlineData("Rust")] // case-sensitive on purpose - no silent partial match
        public void Resolve_FailsClosed_ForAnyUnknownId(string id)
        {
            // Regression: an unrecognised id must never fall through to Rust's page - Rust
            // writes directly to client.cfg, which would be the wrong file for any other game.
            Assert.Equal(GamePageKind.Unsupported, GamePageRouter.Resolve(id));
        }

        [Fact]
        public void EveryCatalogGame_RoutesToItsOwnPage_NotUnsupported()
        {
            // Guards against the catalog growing a game with no matching router case.
            foreach (var game in SupportedGames.All)
                Assert.NotEqual(GamePageKind.Unsupported, GamePageRouter.Resolve(game.Id));
        }
    }
}
