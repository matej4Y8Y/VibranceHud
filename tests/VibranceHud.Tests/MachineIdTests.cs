using VibranceHud.Licensing;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// The id a customer reads off their screen and types into Discord. Every test here is
    /// about that trip surviving: the id they send must be the id the licence binds to.
    /// </summary>
    public sealed class MachineIdTests
    {
        private const string Hash =
            "a1b2c3d4e5f67890a1b2c3d4e5f67890a1b2c3d4e5f67890a1b2c3d4e5f67890";

        [Fact]
        public void An_id_is_four_groups_of_four_upper_case()
        {
            Assert.Equal("A1B2-C3D4-E5F6-7890", MachineId.Format(Hash));
        }

        [Fact]
        public void The_same_hardware_always_produces_the_same_id()
        {
            Assert.Equal(MachineId.Format(Hash), MachineId.Format(Hash));
        }

        [Fact]
        public void Case_and_stray_punctuation_in_the_raw_hash_do_not_change_the_id()
        {
            // The fingerprint has arrived upper case, lower case and hyphenated across builds;
            // any of those must land on the same id or existing licences stop matching.
            Assert.Equal("A1B2-C3D4-E5F6-7890", MachineId.Format(Hash.ToUpperInvariant()));
            Assert.Equal("A1B2-C3D4-E5F6-7890", MachineId.Format("a1b2-c3d4-e5f6-7890-a1b2c3d4e5f67890"));
        }

        [Fact]
        public void Hardware_that_could_not_be_read_gives_an_empty_id_not_a_fake_one()
        {
            // An id invented from nothing would be the same on every such PC, which is one
            // licence unlocking all of them.
            Assert.Equal("", MachineId.Format(null));
            Assert.Equal("", MachineId.Format(""));
            Assert.Equal("", MachineId.Format("   "));
            Assert.Equal("", MachineId.Format("abc"));
            Assert.Equal("", MachineId.Format("zzzzzzzzzzzzzzzzzz"));
        }

        [Theory]
        [InlineData("A1B2-C3D4-E5F6-7890", true)]
        [InlineData("a1b2-c3d4-e5f6-7890", true)]   // typed in lower case
        [InlineData("  A1B2-C3D4-E5F6-7890  ", true)]
        [InlineData("A1B2C3D4E5F67890", false)]     // hyphens missing
        [InlineData("2K7M-Q8XR-T9WD-N3FG", false)]  // that's a key code, not a PC id
        [InlineData("", false)]
        [InlineData(null, false)]
        public void LooksValid_tells_a_pc_id_apart_from_whatever_else_got_pasted(string? input, bool expected)
        {
            Assert.Equal(expected, MachineId.LooksValid(input));
        }
    }
}
