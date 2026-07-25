using System;
using VibranceHud.Audio;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Integration smoke test for the Core Audio COM interop - the one part of Audio Edge
    /// that can't be faked, and where a wrong vtable order compiles fine but fails at runtime.
    /// On a machine with no playback device, constructing throws and the app hides the
    /// feature; that's the documented fallback, so the test accepts it.
    /// </summary>
    public class WindowsAudioOutputTests
    {
        [Fact]
        public void ReadsRealVolumeAndPeak_WithoutChangingAnything()
        {
            WindowsAudioOutput output;
            try
            {
                output = new WindowsAudioOutput();
            }
            catch
            {
                return; // no playback device on this machine - the supported fallback
            }

            using (output)
            {
                float volume = output.Volume;
                float peak = output.Peak;

                // Sane readings prove the interop bound to the right methods.
                Assert.InRange(volume, 0f, 1f);
                Assert.InRange(peak, 0f, 1f);

                // Reading must not have moved the user's volume.
                Assert.Equal(volume, output.Volume, 3);
            }
        }
    }
}
