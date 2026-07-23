using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public class SaturationMatrixTests
    {
        // Rec. 709 luma weights used by the matrix.
        private const float Lr = 0.2126f;
        private const float Lg = 0.7152f;
        private const float Lb = 0.0722f;

        private static float At(float[] m, int row, int col) => m[row * 5 + col];

        [Fact]
        public void Build_ReturnsTwentyFiveEntries()
        {
            Assert.Equal(25, SaturationMatrix.Build(1.0f).Length);
        }

        [Fact]
        public void Saturation_One_IsIdentity()
        {
            var m = SaturationMatrix.Build(1.0f);

            for (int row = 0; row < 5; row++)
            {
                for (int col = 0; col < 5; col++)
                {
                    float expected = row == col ? 1f : 0f;
                    Assert.Equal(expected, At(m, row, col), 4);
                }
            }
        }

        [Fact]
        public void Saturation_Two_MatchesRec709Formula()
        {
            const float s = 2.0f;
            const float a = 1.0f - s; // -1

            var m = SaturationMatrix.Build(s);

            // Row = input channel, col = output channel (row-vector convention).
            // Input R row:
            Assert.Equal(Lr * a + s, At(m, 0, 0), 4);
            Assert.Equal(Lr * a, At(m, 0, 1), 4);
            Assert.Equal(Lr * a, At(m, 0, 2), 4);
            // Input G row:
            Assert.Equal(Lg * a, At(m, 1, 0), 4);
            Assert.Equal(Lg * a + s, At(m, 1, 1), 4);
            Assert.Equal(Lg * a, At(m, 1, 2), 4);
            // Input B row:
            Assert.Equal(Lb * a, At(m, 2, 0), 4);
            Assert.Equal(Lb * a, At(m, 2, 1), 4);
            Assert.Equal(Lb * a + s, At(m, 2, 2), 4);

            // Alpha and offset rows are pass-through identity.
            Assert.Equal(1f, At(m, 3, 3), 4);
            Assert.Equal(1f, At(m, 4, 4), 4);
            Assert.Equal(0f, At(m, 3, 0), 4);
            Assert.Equal(0f, At(m, 4, 1), 4);
        }
    }
}
