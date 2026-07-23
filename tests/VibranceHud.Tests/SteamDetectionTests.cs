using VibranceHud.Games;
using Xunit;

namespace VibranceHud.Tests
{
    public class SteamDetectionTests
    {
        [Fact]
        public void ParseLibraryPaths_NewFormat_ReturnsPaths_IgnoringAppSizes()
        {
            var vdf = @"
""libraryfolders""
{
	""0""
	{
		""path""		""C:\\Program Files (x86)\\Steam""
		""apps""
		{
			""252490""		""12345678""
		}
	}
	""1""
	{
		""path""		""D:\\SteamLibrary""
		""apps""
		{
			""570""		""999""
		}
	}
}";
            var paths = SteamVdf.ParseLibraryPaths(vdf);

            Assert.Contains(@"C:\Program Files (x86)\Steam", paths);
            Assert.Contains(@"D:\SteamLibrary", paths);
            // The numeric app-size entries under "apps" must NOT be picked up as paths.
            Assert.Equal(2, paths.Count);
        }

        [Fact]
        public void ParseLibraryPaths_OldFormat_ReturnsNumberedPaths()
        {
            var vdf = @"
""LibraryFolders""
{
	""TimeNextStatsReport""		""123456""
	""ContentStatsID""		""789""
	""1""		""D:\\SteamLibrary""
	""2""		""E:\\Games\\Steam""
}";
            var paths = SteamVdf.ParseLibraryPaths(vdf);

            Assert.Equal(new[] { @"D:\SteamLibrary", @"E:\Games\Steam" }, paths);
        }

        [Fact]
        public void ParseLibraryPaths_Empty_ReturnsEmpty()
        {
            Assert.Empty(SteamVdf.ParseLibraryPaths(""));
        }

        [Fact]
        public void DetectInstalled_FindsGame_InSecondLibrary()
        {
            var libraries = new[] { @"C:\Steam", @"D:\SteamLibrary" };
            bool FileExists(string p) => p == @"D:\SteamLibrary\steamapps\appmanifest_252490.acf";

            var detected = GameDetection.DetectInstalled(libraries, SupportedGames.All, FileExists);

            var rust = Assert.Single(detected);
            Assert.Equal("Rust", rust.Game.DisplayName);
            Assert.Equal(@"D:\SteamLibrary\steamapps\common\Rust", rust.InstallDir);
        }

        [Fact]
        public void DetectInstalled_NotInstalled_ReturnsEmpty()
        {
            var libraries = new[] { @"C:\Steam" };

            var detected = GameDetection.DetectInstalled(libraries, SupportedGames.All, _ => false);

            Assert.Empty(detected);
        }
    }
}
