using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    /// <summary>
    /// Reproduces exactly what saturation.hlsl computes from a packed matrix, in pure C#, and
    /// checks it against what <see cref="ColorAdjust"/> documents its output should mean.
    ///
    /// This is the check that catches a row/column transpose between the two sides without
    /// needing a GPU: DxShader.PackForShader and the HLSL file have to agree on which axis is
    /// "input channel" and which is "output channel", and nothing in the type system enforces
    /// that - it's two independent hand-written encodings of the same convention.
    /// </summary>
    public sealed class DxShaderPackingTests
    {
        /// <summary>What the pixel shader in saturation.hlsl literally does with the packed
        /// buffer: three dot products, one per output channel, against (r, g, b, 1).</summary>
        private static (float R, float G, float B) RunShader(float[] packed, float r, float g, float b)
        {
            float Dot(int row) =>
                packed[row * 4 + 0] * r + packed[row * 4 + 1] * g + packed[row * 4 + 2] * b + packed[row * 4 + 3];
            return (Dot(0), Dot(1), Dot(2));
        }

        /// <summary>The reference transform: SaturationMatrix/ColorAdjust's own documented
        /// convention, newColor = oldColor * M with M's row = input channel, column = output
        /// channel. Implemented directly from the array rather than reusing any production
        /// helper, so this test can't pass by sharing a bug with the code it's checking.</summary>
        private static (float R, float G, float B) ReferenceTransform(float[] m, float r, float g, float b)
        {
            float NewChannel(int col) => r * m[0 * 5 + col] + g * m[1 * 5 + col] + b * m[2 * 5 + col] + m[4 * 5 + col];
            return (NewChannel(0), NewChannel(1), NewChannel(2));
        }

        [Fact]
        public void Packed_output_matches_the_documented_row_vector_convention()
        {
            // A deliberately asymmetric matrix - real saturation always is one, because
            // Rec. 709's luma weights differ per channel. A packing bug that only shows up
            // on an asymmetric matrix is exactly the kind that a quick "looks fine, it's an
            // identity test" check would miss.
            float[] m = ColorAdjust.Build(
                saturation: 1.3f, vibrance: 1.15f, contrast: 1.08f, brightness: 0.95f, warmth: 0.4f);

            var packed = DxShader.PackForShader(m);

            foreach (var (r, g, b) in new[] { (1f, 0f, 0f), (0f, 1f, 0f), (0f, 0f, 1f), (0.4f, 0.7f, 0.2f) })
            {
                var expected = ReferenceTransform(m, r, g, b);
                var actual = RunShader(packed, r, g, b);

                Assert.Equal(expected.R, actual.R, 4);
                Assert.Equal(expected.G, actual.G, 4);
                Assert.Equal(expected.B, actual.B, 4);
            }
        }

        [Fact]
        public void An_asymmetric_matrix_would_have_failed_the_old_row_major_packing()
        {
            // Pins the regression directly: packs matrix[i*5+0..2] and matrix[i*5+4] straight
            // into row i, the way the code did before. Confirms the two really do disagree
            // whenever the matrix is asymmetric, so this suite would have caught the bug it's
            // named after.
            float[] m = ColorAdjust.Build(
                saturation: 1.3f, vibrance: 1.15f, contrast: 1f, brightness: 1f, warmth: 0f);

            var oldPacking = new float[20];
            for (int i = 0; i < 5; i++)
            {
                oldPacking[i * 4 + 0] = m[i * 5 + 0];
                oldPacking[i * 4 + 1] = m[i * 5 + 1];
                oldPacking[i * 4 + 2] = m[i * 5 + 2];
                oldPacking[i * 4 + 3] = m[i * 5 + 4];
            }

            var expected = ReferenceTransform(m, 0.2f, 0.8f, 0.5f);
            var viaOldPacking = RunShader(oldPacking, 0.2f, 0.8f, 0.5f);

            Assert.NotEqual(expected.G, viaOldPacking.G, 3);
        }

        [Fact]
        public void The_identity_matrix_survives_either_way()
        {
            // The one input where a transpose bug is invisible - which is exactly why the
            // manual "Identity matrix first - verify nothing breaks" smoke test in
            // DxOverlayTests could never have caught this.
            float[] identity =
            {
                1, 0, 0, 0, 0,
                0, 1, 0, 0, 0,
                0, 0, 1, 0, 0,
                0, 0, 0, 1, 0,
                0, 0, 0, 0, 1,
            };

            var packed = DxShader.PackForShader(identity);
            var (r, g, b) = RunShader(packed, 0.3f, 0.6f, 0.9f);

            Assert.Equal(0.3f, r, 5);
            Assert.Equal(0.6f, g, 5);
            Assert.Equal(0.9f, b, 5);
        }
    }
}
