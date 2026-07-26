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
                AddressU = TextureAddress.Clamp,
                AddressV = TextureAddress.Clamp,
                AddressW = TextureAddress.Clamp,
                ComparisonFunction = Comparison.Never,
                MinimumLod = 0,
                MaximumLod = float.MaxValue,
            });
        }

        public void ApplyMatrix(float[] matrix)
        {
            // matrix is 25 floats, row-major. Pack the first four rows into 5 * float4.
            // Each shader row uses .xyz for the RGB coefficients and .w for the translation
            // column (index 4 of that source row).
            var data = new float[20];
            for (int i = 0; i < 5; i++)
            {
                data[i * 4 + 0] = matrix[i * 5 + 0];
                data[i * 4 + 1] = matrix[i * 5 + 1];
                data[i * 4 + 2] = matrix[i * 5 + 2];
                data[i * 4 + 3] = matrix[i * 5 + 4]; // the translation column
            }
            _ctx.UpdateSubresource(data, _matrixBuffer);
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
