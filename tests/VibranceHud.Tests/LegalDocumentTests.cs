using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// The legal documents, checked mechanically.
    ///
    /// These are launch blockers - the roadmap's own words are "don't take a single cent until
    /// they're done" - and the failure mode is silent: a dependency gets added, nobody updates
    /// the notices, and the product ships in breach of a licence nobody read. That last one is
    /// what this file mostly exists to prevent.
    /// </summary>
    public sealed class LegalDocumentTests
    {
        private static string Root() => UiContractTests.RepoRoot();

        private static string Read(string name)
        {
            string path = Path.Combine(Root(), name);
            Assert.True(File.Exists(path), name + " is missing - it is a launch blocker");
            return File.ReadAllText(path);
        }

        [Theory]
        [InlineData("LICENSE.md")]
        [InlineData("THIRD-PARTY-NOTICES.md")]
        [InlineData("PRIVACY.md")]
        [InlineData("EULA.md")]
        public void TheDocumentExistsAndSaysSomething(string name)
        {
            Assert.True(Read(name).Length > 1000, name + " is too short to be a real document");
        }

        /// <summary>
        /// The one that matters. A PackageReference added to the csproj without a matching
        /// entry here means shipping someone else's code with no attribution - and with
        /// NvAPIWrapper under the LGPL, that is a real obligation rather than a courtesy.
        /// </summary>
        [Fact]
        public void EveryPackageWeShipIsDeclaredInTheNotices()
        {
            string csproj = File.ReadAllText(Path.Combine(Root(), "VibranceHud.csproj"));
            string notices = Read("THIRD-PARTY-NOTICES.md");

            var packages = Regex.Matches(csproj, @"PackageReference\s+Include=""([^""]+)""")
                .Select(m => m.Groups[1].Value)
                .ToList();

            Assert.NotEmpty(packages);

            var undeclared = packages
                .Where(p => !notices.Contains(p, System.StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.True(undeclared.Count == 0,
                "These ship with PlexusX but are not in THIRD-PARTY-NOTICES.md:\n  "
                + string.Join("\n  ", undeclared));
        }

        /// <summary>
        /// The version in the notices has to be the version actually referenced, or the file
        /// is describing a licence for code we are not shipping.
        /// </summary>
        [Fact]
        public void TheDeclaredVersionsMatchTheProject()
        {
            string csproj = File.ReadAllText(Path.Combine(Root(), "VibranceHud.csproj"));
            string notices = Read("THIRD-PARTY-NOTICES.md");

            var wrong = Regex.Matches(csproj,
                    @"PackageReference\s+Include=""([^""]+)""\s+Version=""([^""]+)""")
                .Select(m => (Id: m.Groups[1].Value, Version: m.Groups[2].Value))
                .Where(p => !notices.Contains(p.Version))
                .Select(p => $"{p.Id} {p.Version}")
                .ToList();

            Assert.True(wrong.Count == 0,
                "Referenced at a version the notices do not mention:\n  "
                + string.Join("\n  ", wrong));
        }

        /// <summary>
        /// [[LEGAL_ENTITY]] is a deliberate, logged placeholder for the one fact only the owner
        /// has. Any *other* bracket placeholder is someone's unfinished sentence.
        /// </summary>
        [Theory]
        [InlineData("LICENSE.md")]
        [InlineData("THIRD-PARTY-NOTICES.md")]
        [InlineData("PRIVACY.md")]
        [InlineData("EULA.md")]
        public void TheOnlyPlaceholderLeftIsTheLegalEntity(string name)
        {
            var stray = Regex.Matches(Read(name), @"\[\[(\w+)\]\]")
                .Select(m => m.Groups[1].Value)
                .Where(t => t != "LEGAL_ENTITY")
                .Distinct()
                .ToList();

            Assert.True(stray.Count == 0,
                name + " still has unfilled placeholders: " + string.Join(", ", stray));
        }

        /// <summary>
        /// The privacy policy has to name every host the app actually contacts. A policy that
        /// omits an endpoint is worse than no policy - it is a false statement.
        /// </summary>
        [Theory]
        [InlineData("api.github.com")]
        [InlineData("raw.githubusercontent.com")]
        public void ThePrivacyPolicyNamesEveryHostTheAppContacts(string host)
        {
            Assert.Contains(host, Read("PRIVACY.md"));
        }

        /// <summary>
        /// The game-rules clause is the one paragraph in the EULA that protects against the
        /// situation that actually arose - a user banned in a game, blaming the app. It must
        /// not be quietly edited away.
        /// </summary>
        [Fact]
        public void TheEulaKeepsItsGameRulesClause()
        {
            string eula = Read("EULA.md");
            Assert.Contains("overlay", eula, System.StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ban", eula, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
