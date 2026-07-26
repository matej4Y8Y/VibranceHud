using VibranceHud.Games;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Epic Games Launcher manifest (.item JSON) parsing - how Fortnite gets detected.
    /// Pure function; the filesystem walk around it is thin IO.
    /// </summary>
    public class EpicLocatorTests
    {
        private const string FortniteManifest =
            "{\n" +
            "  \"FormatVersion\": 0,\n" +
            "  \"AppName\": \"Fortnite\",\n" +
            "  \"InstallLocation\": \"C:\\\\Program Files\\\\Epic Games\\\\Fortnite\"\n" +
            "}";

        [Fact]
        public void Parses_install_location_for_matching_app()
        {
            var loc = EpicLocator.ParseInstallLocation(FortniteManifest, "Fortnite");
            Assert.Equal("C:\\Program Files\\Epic Games\\Fortnite", loc);
        }

        [Fact]
        public void App_name_match_is_case_insensitive()
        {
            var loc = EpicLocator.ParseInstallLocation(FortniteManifest, "fortnite");
            Assert.Equal("C:\\Program Files\\Epic Games\\Fortnite", loc);
        }

        [Fact]
        public void Returns_null_for_a_different_app()
        {
            Assert.Null(EpicLocator.ParseInstallLocation(FortniteManifest, "RocketLeague"));
        }

        [Fact]
        public void Returns_null_for_malformed_json()
        {
            Assert.Null(EpicLocator.ParseInstallLocation("{ not json", "Fortnite"));
        }

        [Fact]
        public void Returns_null_when_install_location_missing()
        {
            Assert.Null(EpicLocator.ParseInstallLocation("{\"AppName\":\"Fortnite\"}", "Fortnite"));
        }

        [Fact]
        public void Fortnite_is_registered_as_an_epic_game_not_steam()
        {
            var fn = SupportedGames.Fortnite;
            Assert.Equal("Fortnite", fn.EpicAppName);
            Assert.Equal(0, fn.SteamAppId);
        }

        [Fact]
        public void Apex_is_registered_with_its_steam_appid()
        {
            Assert.Equal(1172470, SupportedGames.Apex.SteamAppId);
            Assert.Null(SupportedGames.Apex.EpicAppName);
        }

        [Fact]
        public void Returns_null_for_valid_json_with_wrong_root_shape()
        {
            // A corrupted-but-valid .item file must not throw (never-throws contract).
            Assert.Null(EpicLocator.ParseInstallLocation("[1,2,3]", "Fortnite"));
            Assert.Null(EpicLocator.ParseInstallLocation("\"Fortnite\"", "Fortnite"));
            Assert.Null(EpicLocator.ParseInstallLocation("null", "Fortnite"));
        }

        [Fact]
        public void Returns_null_for_non_string_fields()
        {
            Assert.Null(EpicLocator.ParseInstallLocation("{\"AppName\":123}", "Fortnite"));
            Assert.Null(EpicLocator.ParseInstallLocation("{\"AppName\":\"Fortnite\",\"InstallLocation\":42}", "Fortnite"));
        }
    }
}
