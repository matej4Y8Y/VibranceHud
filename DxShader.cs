using System;
using System.IO;
using System.Reflection;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;

namespace VibranceHud
{
    /// <summary>
    /// Loads saturation.hlsl from the embedded resource, compiles it at runtime, and
    /// exposes ApplyMatrix/Bind/Draw to render a full-screen quad that samples the captured
    /// desktop and multiplies each pixel by the 5x5 color matrix (packed into cbuffer b0).
    ///
    /// If D3DCompile fails, the constructor throws - per the spec this is a build error,
    /// not a runtime one, so we fail fast rather than silently fall back.
    /// </summary>
    internal sealed class DxShader : IDisposable
    {
        private readonly VertexShader _vs;
        private readonly PixelShader _ps;
        private readonly InputLayout _layout;
        private readonly Buffer _matrixBuffer;
        private readonly Buffer _vertexBuffer;
        private readonly SamplerState _sampler;
        private readonly DeviceContext _ctx;

        // pos (float4) + uv (float2) = 6 floats = 24 bytes per vertex.
        private const int VertexStride = 24;

        public DxShader(Device device, DeviceContext ctx)
        {
            _ctx = ctx;
            var psBytes = LoadEmbeddedHLSL();
            using (var psBytecode = new ShaderBytecode(psBytes))
            {
                _ps = new PixelShader(device, psBytecode);
            }

            // Pass-through vertex shader; the vertex buffer already carries clip-space
            // positions for a full-screen quad, so no transform is needed.
            var vsSrc = "struct VS_IN { float4 pos:POSITION; float2 uv:TEXCOORD; }; " +
                         "struct VS_OUT { float4 pos:SV_POSITION; float2 uv:TEXCOORD; }; " +
                         "VS_OUT main(VS_IN i) { VS_OUT o; o.pos = i.pos; o.uv = i.uv; return o; }";
            using (var vsBytecode = ShaderBytecode.Compile(vsSrc, "main", "vs_5_0"))
            {
                _vs = new VertexShader(device, vsBytecode);
                _layout = new InputLayout(device, vsBytecode,
                    new[]
                    {
                        new InputElement("POSITION", 0, SharpDX.DXGI.Format.R32G32B32A32_Float, 0, 0),
                        new InputElement("TEXCOORD", 0, SharpDX.DXGI.Format.R32G32_Float, 16, 0),
                    });
            }

            _matrixBuffer = new Buffer(device, new BufferDescription
            {
                SizeInBytes = 80, // 5 * float4 = 80 bytes
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.ConstantBuffer,
                CpuAccessFlags = CpuAccessFlags.None,
            });

            // Two triangles covering clip space [-1,1] with UVs [0,1]. In clip space +Y is
            // up but texture V grows downward, so V is flipped (top-left of the texture maps
            // to the top-left of the screen).
            float[] verts =
            {
                //   x,     y,    z,   w,     u,   v
                -1f,  1f, 0f, 1f,   0f, 0f, // top-left
                 1f,  1f, 0f, 1f,   1f, 0f, // top-right
                -1f, -1f, 0f, 1f,   0f, 1f, // bottom-left
                 1f,  1f, 0f, 1f,   1f, 0f, // top-right
                 1f, -1f, 0f, 1f,   1f, 1f, // bottom-right
                -1f, -1f, 0f, 1f,   0f, 1f, // bottom-left
            };
            _vertexBuffer = Buffer.Create(device, BindFlags.VertexBuffer, verts);

            _sampler = new SamplerState(device, new SamplerStateDescription
            {
                Filter = Filter.MinMagMipLinear,
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                ComparisonFunction = Comparison.Never,
                MinimumLod = 0,
                MaximumLod = float.MaxValue,
            });
        }

        public void ApplyMatrix(float[] matrix)
        {
            _ctx.UpdateSubresource(PackForShader(matrix), _matrixBuffer);
        }

        /// <summary>
        /// Repack a 25-float row-major colour matrix into the three float4 rows
        /// saturation.hlsl actually consumes.
        ///
        /// SaturationMatrix/ColorAdjust document and use the row-vector convention:
        /// <c>newColor = oldColor * M</c>, where M's ROW is the input channel and COLUMN is
        /// the output channel - the same layout Windows' own MAGCOLOREFFECT expects, which is
        /// why the Magnification path (the one actually shipping today) has always produced
        /// the right picture.
        ///
        /// The shader does the opposite: for each output channel it takes the dot product of
        /// one float4 against <c>(r, g, b, 1)</c> - i.e. <c>newColor = M * oldColor</c>, where
        /// a ROW of the packed data is one OUTPUT channel's coefficients. Packing straight
        /// rows-to-rows (the previous implementation) fed row-major data into a
        /// column-major consumer: correct only when the upper 3x3 block happens to be
        /// symmetric, which real saturation/vibrance values never are - Rec. 709's luma
        /// weights differ per channel, so the cross-terms differ depending on which side of
        /// the diagonal they're on. The visible result would have been channels blending
        /// into each other on the wrong axis, not simply "no effect".
        ///
        /// Never exercised in production: the DX11 path this shader belongs to has been
        /// switched off since it was written (see DxDevice's AlphaMode note), so this bug has
        /// never shipped. Fixed now so it isn't waiting for whoever revives that path.
        ///
        /// Pure and internal so it's testable without a Direct3D device.
        /// </summary>
        internal static float[] PackForShader(float[] matrix)
        {
            var data = new float[20];
            for (int outCol = 0; outCol < 3; outCol++)
            {
                data[outCol * 4 + 0] = matrix[0 * 5 + outCol];   // R's contribution
                data[outCol * 4 + 1] = matrix[1 * 5 + outCol];   // G's contribution
                data[outCol * 4 + 2] = matrix[2 * 5 + outCol];   // B's contribution
                data[outCol * 4 + 3] = matrix[4 * 5 + outCol];   // the translation row
            }
            // Rows 3 (output slot for alpha, which the shader passes through untouched
            // instead of reading from here) and 4 are never read by saturation.hlsl; left
            // zeroed rather than packed with anything that could look meaningful.
            return data;
        }

        public void Bind(ShaderResourceView capturedFrame)
        {
            _ctx.InputAssembler.InputLayout = _layout;
            _ctx.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            _ctx.InputAssembler.SetVertexBuffers(0,
                new VertexBufferBinding(_vertexBuffer, VertexStride, 0));

            _ctx.VertexShader.Set(_vs);

            _ctx.PixelShader.Set(_ps);
            _ctx.PixelShader.SetConstantBuffer(0, _matrixBuffer);
            _ctx.PixelShader.SetSampler(0, _sampler);
            _ctx.PixelShader.SetShaderResource(0, capturedFrame);
        }

        /// <summary>Draw the full-screen quad (6 vertices, two triangles).</summary>
        public void Draw()
        {
            _ctx.Draw(6, 0);
        }

        public void Dispose()
        {
            _vs.Dispose();
            _ps.Dispose();
            _layout.Dispose();
            _matrixBuffer.Dispose();
            _vertexBuffer.Dispose();
            _sampler.Dispose();
        }

        private static byte[] LoadEmbeddedHLSL()
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("PlexusX.saturation.hlsl");
            if (stream == null) throw new InvalidOperationException("saturation.hlsl not embedded");
            using var reader = new StreamReader(stream);
            var src = reader.ReadToEnd();
            using var bytecode = ShaderBytecode.Compile(src, "main", "ps_5_0");
            var bytes = new byte[bytecode.Bytecode.Data.Length];
            Array.Copy(bytecode.Bytecode.Data, bytes, bytes.Length);
            return bytes;
        }
    }
}
