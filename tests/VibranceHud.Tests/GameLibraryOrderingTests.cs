using System.Linq;
using VibranceHud.Games;
using Xunit;

namespace VibranceHud.Tests
{
    public class GameLibraryOrderingTests
    {
        [Fact]
        public void OrderForHub_AllInstalled_ReturnsCatalogOrder()
        {
            var detected = SupportedGames.All.Select(g => new DetectedGame(g, @"C:\fake")).ToList();

            var ordered = GameLibrary.OrderForHub(detected);

            Assert.Equal(SupportedGames.All, ordered.Select(x => x.Game).ToList());
            Assert.All(ordered, x => Assert.NotNull(x.Detected));
        }

        [Fact]
        public void OrderForHub_NoneInstalled_ReturnsCatalogOrder_AllUndetected()
        {
            var ordered = GameLibrary.OrderForHub(new DetectedGame[0]);

            Assert.Equal(SupportedGames.All, ordered.Select(x => x.Game).ToList());
            Assert.All(ordered, x => Assert.Null(x.Detected));
        }

        [Fact]
        public void OrderForHub_PartiallyInstalled_InstalledFirst_ThenCatalogOrderWithinEachGroup()
        {
            var detected = new[]
            {
                new DetectedGame(SupportedGames.Apex, @"C:\apex"),
                new DetectedGame(SupportedGames.Rust, @"C:\rust"),
            };

            var ordered = GameLibrary.OrderForHub(detected);

            // Installed group keeps catalog order (Rust before Apex), then not-installed
            // group keeps catalog order (Cs2 before Fortnite).
            Assert.Equal(
                new[] { SupportedGames.Rust, SupportedGames.Apex, SupportedGames.Cs2, SupportedGames.Fortnite },
                ordered.Select(x => x.Game).ToList());
            Assert.NotNull(ordered[0].Detected);
            Assert.NotNull(ordered[1].Detected);
            Assert.Null(ordered[2].Detected);
            Assert.Null(ordered[3].Detected);
        }
    }
}
