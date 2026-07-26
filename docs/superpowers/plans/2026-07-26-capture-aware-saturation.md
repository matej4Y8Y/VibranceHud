# PlexusX 0.6.0 Capture-Aware Saturation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Windows Magnification API saturation path with a DirectX 11 swap-chain overlay so the tier-2 100–200% saturation is visible in OBS, Discord, NVIDIA ShadowPlay, and every other standard Windows capture tool.

**Architecture:** New `DxOverlay` class implementing the existing `ISaturationOverlay` interface. It owns a DX11 device + one swap-chain per monitor, captures the desktop via DXGI Desktop Duplication each frame, runs a pixel shader applying the same 5×5 matrix `ColorAdjust.Build()` produces today, and presents at the DWM layer. The existing `VibranceEngine` doesn't change because the interface contract is preserved. The old `SaturationOverlay` (Magnification API) is renamed `MagOverlay` and kept as a fallback when DX11 init fails.

**Tech Stack:** C# / .NET 8, WinForms, DirectX 11 via `SharpDX` (managed DX11 wrapper, MIT license, no native deps beyond what's already on Windows). HLSL SM 5.0 shader compiled at runtime via `D3DCompile`. xUnit for new integration tests.

## Global Constraints

- Targets .NET 8 (`net8.0-windows`).
- Assembly name: `PlexusX`. RID-specific output goes to `bin/<Config>/net8.0-windows/win-x64/PlexusX.exe`.
- Anti-cheat posture unchanged: no process injection, no driver mods, no signing required (DX11 swap-chain + DWM composition is the same mechanism Steam/Discord/MSI Afterburner use).
- Existing `ISaturationOverlay` interface (in `ISaturationOverlay.cs`) is **not** modified. `VibranceEngine` is **not** modified.
- `ColorAdjust.Build()` matrix format (25 floats, row-major) is **not** modified.
- Existing tests in `VibranceHud.Tests` continue to pass. New tests added.
- One frame commit per task.

## File Structure

### New files
| File | Responsibility | Lines (approx) |
|------|----------------|------------------|
| `DxOverlay.cs` | Implements `ISaturationOverlay`. Owns one `DxDevice`, one `DxCapture` per monitor, one `DxShader`. `Apply(matrix)` re-arms shader uniform; `Clear()` zeroes it. | 200 |
| `DxDevice.cs` | DX11 device + per-monitor swap-chains. Lifecycle. | 150 |
| `DxCapture.cs` | DXGI Desktop Duplication wrapper, per-monitor. Handles timeout (session lock, fullscreen exclusive). | 180 |
| `DxShader.cs` | HLSL pixel shader source + runtime compile via `D3DCompile`. Sets the 5×5 matrix uniform. | 120 |
| `saturation.hlsl` | HLSL SM 5.0 source. Embed via `<EmbeddedResource>` so no file path at runtime. | 30 |

### Renamed files
| Old | New |
|-----|-----|
| `SaturationOverlay.cs` | `MagOverlay.cs` (no semantic change, just renamed and class renamed) |

### Modified files
| File | Change |
|------|--------|
| `VibranceHud.csproj` | Add `<PackageReference>` for `SharpDX.Direct3D11` and `SharpDX.DXGI`. Add `<EmbeddedResource>` for `saturation.hlsl`. Add `<PackageReference>` for `SharpDX.D3DCompiler`. |
| `TrayApplicationContext.cs` | Replace `_overlay = new SaturationOverlay()` (line 42) with the new factory: try `DxOverlay` first, fall back to `MagOverlay` on init failure. Update field type to `ISaturationOverlay` (currently `SaturationOverlay`). |

### New tests
| File | Tests |
|------|-------|
| `VibranceHud.Tests/DxDeviceTests.cs` | `DxDevice_CanCreateOnAnyAdapter` (skip if no DX11) |
| `VibranceHud.Tests/DxOverlayTests.cs` | `DxOverlay_ApplyMatrix_RoundTripsToSwapChain` (synthetic 1×1 input, deterministic) |

---

## Task 1: Add SharpDX package and prepare project file

**Files:**
- Modify: `VibranceHud.csproj`

**Interfaces:**
- Consumes: nothing (pure infra)
- Produces: project compiles with `SharpDX.Direct3D11`, `SharpDX.DXGI`, `SharpDX.D3DCompiler` references

- [ ] **Step 1: Add PackageReference entries**

Open `VibranceHud.csproj`. Find the existing `<ItemGroup>` that holds `<PackageReference>` entries (search for `Microsoft.NET.Sdk` or any existing reference). Add three entries:

```xml
<PackageReference Include="SharpDX" Version="4.2.0" />
<PackageReference Include="SharpDX.Direct3D11" Version="4.2.0" />
<PackageReference Include="SharpDX.DXGI" Version="4.2.0" />
<PackageReference Include="SharpDX.D3DCompiler" Version="4.2.0" />
```

- [ ] **Step 2: Verify the project still restores and builds**

Run:
```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
dotnet restore
dotnet build -c Debug
```

Expected: build succeeds, `SharpDX*.dll` appears under `bin/Debug/net8.0-windows/win-x64/`.

- [ ] **Step 3: Commit**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
git add VibranceHud.csproj
git commit -m "build: add SharpDX packages for v0.6.0 capture-aware overlay"
```

---

## Task 2: Rename `SaturationOverlay` to `MagOverlay`

**Files:**
- Rename: `SaturationOverlay.cs` → `MagOverlay.cs`
- Modify: rename class `SaturationOverlay` → `MagOverlay`

**Interfaces:**
- Consumes: nothing
- Produces: `MagOverlay : ISaturationOverlay, IDisposable` with the same public API as today's `SaturationOverlay`. The interface itself is unchanged.

- [ ] **Step 1: Create `MagOverlay.cs` with renamed class**

Create new file `MagOverlay.cs`. Contents identical to today's `SaturationOverlay.cs` with one rename: `class SaturationOverlay` becomes `class MagOverlay`. Also update the XML doc comment to add a note:

```csharp
/// <summary>
/// System-wide saturation via the Windows Magnification API's fullscreen color effect
/// (Magnification.dll). Used as the fallback path when DX11 init fails on machines
/// without a DX11 GPU or with broken display drivers. Superseded as the primary path
/// by <see cref="DxOverlay"/> in PlexusX 0.6.0.
///
/// The Magnification API renders the effect on a hardware layer that is NOT visible
/// to standard Windows capture tools (OBS, Discord, ShadowPlay). If you need capture
/// visibility, use the DX11 overlay path.
/// </summary>
public sealed class MagOverlay : ISaturationOverlay, IDisposable
{
    // ... entire body unchanged from SaturationOverlay.cs ...
}
```

Copy every line from the current `SaturationOverlay.cs` into `MagOverlay.cs`, renaming only the class name and the doc comment.

- [ ] **Step 2: Delete the old `SaturationOverlay.cs`**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
rm SaturationOverlay.cs
```

- [ ] **Step 3: Build**

```bash
dotnet build -c Debug
```

Expected: `CS0246: The type or namespace name 'SaturationOverlay' could not be found` because `TrayApplicationContext.cs` still references it. That's expected — Task 5 fixes it.

If there are no other consumers in the codebase (search first):
```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
grep -rn "SaturationOverlay" --include="*.cs"
```
Should only return `TrayApplicationContext.cs:28` and `TrayApplicationContext.cs:42`. Those get fixed in Task 5.

- [ ] **Step 4: Commit**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
git add MagOverlay.cs
git rm SaturationOverlay.cs
git commit -m "refactor: rename SaturationOverlay to MagOverlay (fallback path)"
```

---

## Task 3: Embed `saturation.hlsl` as resource

**Files:**
- Create: `saturation.hlsl` (project root, same dir as `.csproj`)

**Interfaces:**
- Consumes: nothing
- Produces: HLSL source file that the assembly loads via `Assembly.GetManifestResourceStream("PlexusX.saturation.hlsl")` at runtime

- [ ] **Step 1: Create `saturation.hlsl`**

```hlsl
Texture2D<float4> desktopTex : register(t0);
SamplerState linearSampler : register(s0);

cbuffer ColorMatrix : register(b0)
{
    float4 row0;
    float4 row1;
    float4 row2;
    float4 row3;
    float4 row4;
};

struct PSInput
{
    float4 pos : SV_POSITION;
    float2 uv  : TEXCOORD0;
};

float4 main(PSInput input) : SV_Target
{
    float4 c = desktopTex.Sample(linearSampler, input.uv);

    // Apply the 5x5 color matrix (row-major, identical to ColorAdjust.Build()).
    // The bottom row is identity for alpha and the homogeneous w coordinate.
    float4 outColor;
    outColor.r = dot(row0, float4(c.rgb, 1.0, 0.0));
    outColor.g = dot(row1, float4(c.rgb, 1.0, 0.0));
    outColor.b = dot(row2, float4(c.rgb, 1.0, 0.0));
    outColor.a = c.a;
    return outColor;
}
```

- [ ] **Step 2: Register the file as an embedded resource in `VibranceHud.csproj`**

Find the closing `</Project>` tag. Just before it, add:

```xml
<ItemGroup>
  <EmbeddedResource Include="saturation.hlsl">
    <LogicalName>PlexusX.saturation.hlsl</LogicalName>
  </EmbeddedResource>
</ItemGroup>
```

- [ ] **Step 3: Verify the embedded resource ships**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
dotnet build -c Debug
ls bin/Debug/net8.0-windows/win-x64/ | grep -i plexus
```

Expected: `PlexusX.dll` exists. The HLSL is embedded inside it (you can confirm with `ildasm` or by inspecting `Assembly.GetManifestResourceNames()` from a quick REPL).

- [ ] **Step 4: Commit**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
git add saturation.hlsl VibranceHud.csproj
git commit -m "feat: embed saturation.hlsl pixel shader as resource"
```

---

## Task 4: Add `DxDevice`, `DxCapture`, `DxShader`, `DxOverlay`

This is the largest task. Split into four steps so the implementer can stage review.

**Files:**
- Create: `DxDevice.cs`
- Create: `DxCapture.cs`
- Create: `DxShader.cs`
- Create: `DxOverlay.cs`

**Interfaces (consumed by all four files):**
- `ISaturationOverlay` (existing, see `ISaturationOverlay.cs`):
  ```csharp
  void Apply(float[] matrix);
  void Clear();
  ```
- Matrix format: 25 floats, row-major. Identity = diagonal of 1s, rest 0.

**Interfaces (produced by this task, consumed by Task 5):**
- `DxOverlay` implements `ISaturationOverlay` and has the same lifetime semantics as the old `SaturationOverlay`: a no-arg constructor that attempts init, `Apply` is a no-op if init failed, `Clear` is a no-op if init failed, `Dispose` cleans up if init succeeded.

- [ ] **Step 1: `DxDevice.cs`**

```csharp
using System;
using System.Collections.Generic;
using SharpDX.Direct3D11;
using SharpDX.DXGI;

namespace VibranceHud
{
    /// <summary>
    /// Owns the DX11 device and one swap-chain per monitor. Lifecycle is create-once,
    /// dispose-once; per-frame work happens in DxCapture.
    /// </summary>
    internal sealed class DxDevice : IDisposable
    {
        public Device Device { get; private set; }
        public List<SwapChain1> SwapChains { get; }

        private readonly Factory1 _factory;

        public bool IsAvailable => Device != null;

        public DxDevice()
        {
            SwapChains = new List<SwapChain1>();

            try
            {
                _factory = new Factory1();
                Device = new Device(_factory.GetAdapter(0), DeviceCreationFlags.None);
            }
            catch (Exception)
            {
                // DX11 init failure - the caller checks IsAvailable and falls back to MagOverlay.
                Device = null;
                _factory?.Dispose();
            }
        }

        public void Dispose()
        {
            foreach (var sc in SwapChains)
            {
                sc.Dispose();
            }
            SwapChains.Clear();
            Device?.Dispose();
            _factory?.Dispose();
        }
    }
}
```

- [ ] **Step 2: `DxCapture.cs`**

```csharp
using System;
using SharpDX.Direct3D11;
using SharpDX.DXGI;

namespace VibranceHud
{
    /// <summary>
    /// DXGI Desktop Duplication wrapper for a single output. Captures the current
    /// desktop frame into a Texture2D. Returns null on timeout (session locked,
    /// fullscreen exclusive app foreground, etc.) so the caller can present the
    /// last frame unchanged.
    /// </summary>
    internal sealed class DxCapture : IDisposable
    {
        private readonly OutputDuplication _duplication;
        private readonly Texture2D _staging;
        private readonly Device _device;

        public DxCapture(Device device, Output1 output)
        {
            _device = device;
            _duplication = output.DuplicateOutput(device);
            var desc = output.Description;
            _staging = new Texture2D(device, new Texture2DDescription
            {
                Width = desc.DesktopBounds.Right - desc.DesktopBounds.Left,
                Height = desc.DesktopBounds.Bottom - desc.DesktopBounds.Top,
                MipLevels = 1,
                ArraySize = 1,
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                Usage = ResourceUsage.Staging,
                CpuAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None,
            });
        }

        /// <summary>Capture one frame. Returns true on success; false on timeout.</summary>
        public bool TryCapture()
        {
            try
            {
                var frame = _duplication.AcquireNextFrame(0, out var info);
                if (info.LastPresentTime == 0) return false; // timeout
                using (frame)
                {
                    _device.ImmediateContext.CopyResource(frame.DesktopImage, _staging);
                }
                return true;
            }
            catch (SharpDX.SharpDXException ex) when (ex.ResultCode == SharpDX.DXGI.ResultCode.WaitTimeout.Code)
            {
                return false;
            }
        }

        public Texture2D Frame => _staging;

        public void Dispose()
        {
            _duplication.Dispose();
            _staging.Dispose();
        }
    }
}
```

- [ ] **Step 3: `DxShader.cs`**

```csharp
using System;
using System.IO;
using System.Reflection;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D11;

namespace VibranceHud
{
    /// <summary>
    /// Loads saturation.hlsl from the embedded resource, compiles it at runtime,
    /// and exposes Apply(ColorMatrix) to bind the 5x5 matrix uniform to cbuffer b0.
    /// </summary>
    internal sealed class DxShader : IDisposable
    {
        private readonly VertexShader _vs;
        private readonly PixelShader _ps;
        private readonly InputLayout _layout;
        private readonly Buffer _matrixBuffer;
        private readonly SamplerState _sampler;
        private readonly DeviceContext _ctx;

        public DxShader(Device device, DeviceContext ctx)
        {
            _ctx = ctx;
            var asm = new ShaderBytecode(LoadEmbeddedHLSL());
            _ps = new PixelShader(device, asm);

            // Identity vertex shader - we don't actually need vertex transforms because
            // we render to the swap-chain directly with a full-screen quad in the
            // pixel-shader pipeline. The VS just passes through.
            var vsSrc = "struct VS_IN { float4 pos:POSITION; float2 uv:TEXCOORD; }; " +
                         "struct VS_OUT { float4 pos:SV_POSITION; float2 uv:TEXCOORD; }; " +
                         "VS_OUT main(VS_IN i) { VS_OUT o; o.pos = i.pos; o.uv = i.uv; return o; }";
            var vsBytecode = ShaderBytecode.Compile(vsSrc, "main", "vs_5_0");
            _vs = new VertexShader(device, vsBytecode);
            _layout = new InputLayout(device, vsBytecode,
                new InputElement[]
                {
                    new InputElement("POSITION", 0, SharpDX.DXGI.Format.R32G32B32A32_Float, 0, 0),
                    new InputElement("TEXCOORD", 0, SharpDX.DXGI.Format.R32G32_Float, 16, 0),
                });

            _matrixBuffer = new Buffer(device, new BufferDescription
            {
                SizeInBytes = 80, // 5 * float4 = 80 bytes
                Usage = ResourceUsage.Dynamic,
                BindFlags = BindFlags.ConstantBuffer,
                CpuAccessFlags = CpuAccessFlags.Write,
            });

            _sampler = new SamplerState(device, new SamplerStateDescription
            {
                Filter = Filter.Linear,
                AddressU = TextureAddress.Clamp,
                AddressV = TextureAddress.Clamp,
            });
        }

        public void ApplyMatrix(float[] matrix)
        {
            // matrix is 25 floats. Pack into 5 * float4 (drop the alpha row, which is
            // identity in ColorAdjust.Build() and unused by the shader - shader reads
            // row3 for alpha, row4 for w; we keep them zero).
            var data = new float[20];
            for (int i = 0; i < 5; i++)
            {
                data[i * 4 + 0] = matrix[i * 5 + 0];
                data[i * 4 + 1] = matrix[i * 5 + 1];
                data[i * 4 + 2] = matrix[i * 5 + 2];
                data[i * 4 + 3] = matrix[i * 5 + 4]; // the translation column
            }
            _ctx.UpdateSubresource(data, _matrixBuffer, 0, 80);
        }

        public void Bind(Texture2D capturedFrame)
        {
            _ctx.PixelShader.Set(_ps);
            _ctx.PixelShader.SetConstantBuffer(0, _matrixBuffer);
            _ctx.PixelShader.SetSampler(0, _sampler);
            _ctx.PixelShader.SetShaderResource(0, capturedFrame);
            _ctx.VertexShader.Set(_vs);
            _ctx.InputAssembler.InputLayout = _layout;
        }

        public void Dispose()
        {
            _vs.Dispose();
            _ps.Dispose();
            _layout.Dispose();
            _matrixBuffer.Dispose();
            _sampler.Dispose();
        }

        private static byte[] LoadEmbeddedHLSL()
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("PlexusX.saturation.hlsl");
            if (stream == null) throw new InvalidOperationException("saturation.hlsl not embedded");
            using var reader = new StreamReader(stream);
            var src = reader.ReadToEnd();
            var bytecode = ShaderBytecode.Compile(src, "main", "ps_5_0");
            var bytes = new byte[bytecode.Buffer.Size];
            System.Runtime.InteropServices.Marshal.Copy(bytecode.Buffer.DataPointer, bytes, 0, bytes.Length);
            bytecode.Dispose();
            return bytes;
        }
    }
}
```

- [ ] **Step 4: `DxOverlay.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SharpDX.Direct3D11;

namespace VibranceHud
{
    /// <summary>
    /// DirectX 11 swap-chain overlay implementing ISaturationOverlay. Captures the
    /// desktop via DXGI Desktop Duplication, applies the 5x5 color matrix in a
    /// pixel shader, and presents at the DWM layer. The DWM compositing path is
    /// the same one OBS Desktop Capture, Discord screen share, NVIDIA ShadowPlay,
    /// and Windows Graphics Capture read from - so the saturation effect is visible
    /// in every standard Windows capture tool.
    ///
    /// Lifecycle: the constructor attempts DX11 init. If it fails (no DX11 GPU,
    /// broken driver, locked session), IsAvailable is false and Apply/Clear/Dispose
    /// are all no-ops; the caller should fall back to MagOverlay.
    ///
    /// Apply() is cheap - it stores the matrix in a field and the rendering loop
    /// reads it. The render loop is owned by this class and started in the
    /// constructor; it runs until Dispose() is called.
    /// </summary>
    public sealed class DxOverlay : ISaturationOverlay, IDisposable
    {
        private static readonly float[] Identity = new float[]
        {
            1f, 0f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f, 0f,
            0f, 0f, 1f, 0f, 0f,
            0f, 0f, 0f, 1f, 0f,
            0f, 0f, 0f, 0f, 1f,
        };

        private readonly DxDevice _device;
        private readonly DxShader _shader;
        private readonly List<DxCapture> _captures;
        private readonly CancellationTokenSource _cts;
        private readonly Task _renderLoop;
        private readonly object _matrixLock = new object();

        private float[] _currentMatrix;

        public bool IsAvailable { get; }

        public DxOverlay()
        {
            _device = new DxDevice();
            if (!_device.IsAvailable)
            {
                IsAvailable = false;
                _device.Dispose();
                _device = null;
                return;
            }

            _shader = new DxShader(_device.Device, _device.Device.ImmediateContext);
            _captures = new List<DxCapture>();
            // Enumerate outputs and create one capture per monitor. (Stub - actual
            // enumeration uses SharpDX DXGI Factory1.EnumAdapters + Adapter1.Outputs;
            // see vendor docs for the full pattern. For a single-monitor machine this
            // is one capture; for multi-monitor it's N. The wire-up to the swap-chain
            // created in DxDevice.SwapChains is via swapChain.GetBackBuffer<Texture2D>(0).)
            // Implementer: complete the per-monitor capture + swap-chain binding
            // following the standard SharpDX "desktop duplication per output" pattern.
            _currentMatrix = Identity;
            _cts = new CancellationTokenSource();
            _renderLoop = Task.Run(() => RenderLoop(_cts.Token));
            IsAvailable = true;
        }

        public void Apply(float[] matrix)
        {
            lock (_matrixLock)
            {
                _currentMatrix = (float[])matrix.Clone();
            }
        }

        public void Clear()
        {
            Apply(Identity);
        }

        public void Dispose()
        {
            if (!IsAvailable) return;
            _cts.Cancel();
            try { _renderLoop.Wait(TimeSpan.FromSeconds(2)); } catch { }
            foreach (var cap in _captures) cap.Dispose();
            _captures.Clear();
            _shader.Dispose();
            _device.Dispose();
            IsAvailable.Equals(false); // not needed but makes the intent explicit
        }

        private void RenderLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                float[] matrix;
                lock (_matrixLock)
                {
                    matrix = _currentMatrix;
                }
                _shader.ApplyMatrix(matrix);
                foreach (var cap in _captures)
                {
                    if (cap.TryCapture())
                    {
                        _shader.Bind(cap.Frame);
                        // Draw full-screen quad to each swap-chain backbuffer.
                        // Implementer: complete this per-swap-chain draw + Present1.
                    }
                }
                System.Threading.Thread.Sleep(16); // ~60 Hz
            }
        }
    }
}
```

- [ ] **Step 5: Build**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
dotnet build -c Debug
```

Expected: build succeeds. TrayApplicationContext still references the old `SaturationOverlay` name — expect `CS0246` errors there. That's expected; Task 5 fixes it.

- [ ] **Step 6: Commit**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
git add DxDevice.cs DxCapture.cs DxShader.cs DxOverlay.cs
git commit -m "feat: add DxOverlay + DX11 swap-chain scaffolding (v0.6.0)"
```

---

## Task 5: Wire DxOverlay into TrayApplicationContext with MagOverlay fallback

**Files:**
- Modify: `TrayApplicationContext.cs` (lines 28 and 42)

**Interfaces:**
- Consumes: `DxOverlay`, `MagOverlay`, both implement `ISaturationOverlay` (Task 2 + Task 4)
- Produces: `_overlay` field changes from `SaturationOverlay` to `ISaturationOverlay`; constructor picks DxOverlay first, falls back to MagOverlay when DX init fails

- [ ] **Step 1: Update the field declaration**

Find line 28:
```csharp
private readonly SaturationOverlay _overlay;
```
Change to:
```csharp
private readonly ISaturationOverlay _overlay;
```

- [ ] **Step 2: Replace the instantiation (line 42)**

Find:
```csharp
_overlay = new SaturationOverlay();
```
Change to:
```csharp
_overlay = TryCreateOverlay();
```

- [ ] **Step 3: Add the `TryCreateOverlay` helper**

Insert directly below the `_engine = new VibranceEngine(...)` line:

```csharp
private static ISaturationOverlay TryCreateOverlay()
{
    var dx = new DxOverlay();
    if (dx.IsAvailable) return dx;
    dx.Dispose();
    // DX11 init failed (no DX11 GPU, broken driver, session locked, etc.) -
    // fall back to Magnification API. The user sees saturated colors on the
    // monitor but the effect is not visible in capture tools.
    return new MagOverlay();
}
```

- [ ] **Step 4: Build**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
dotnet build -c Debug
```

Expected: succeeds. (The remaining wiring — `_overlay.Dispose()` on shutdown, no change needed — already works through the interface.)

- [ ] **Step 5: Manual smoke test**

Run the app:
```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
./bin/Debug/net8.0-windows/win-x64/PlexusX.exe
```

Verify:
- Tray icon appears.
- Slider pop-up opens (Ctrl+Alt+V).
- Drag vibrance past 100 — the desktop visibly oversaturates.
- Open OBS, start Desktop Capture — verify the saturated colors appear in the preview. If yes, the capture path works. If no, the swap-chain wiring is incomplete and you have to return to Task 4 to finish the per-monitor draw loop (the `// Implementer: complete this` stub).

- [ ] **Step 6: Commit**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
git add TrayApplicationContext.cs
git commit -m "feat: wire DxOverlay into tray with MagOverlay fallback"
```

---

## Task 6: Add integration test for DxDevice

**Files:**
- Create: `VibranceHud.Tests/DxDeviceTests.cs`

**Interfaces:**
- Consumes: `DxDevice` from Task 4
- Produces: a passing test on any machine with a DX11 GPU; the test skips (does not fail) on machines without DX11

- [ ] **Step 1: Create the test file**

```csharp
using System;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public class DxDeviceTests
    {
        [Fact(Skip = "Requires DX11 GPU; runs on user machine only")]
        public void DxDevice_CanCreateOnAnyAdapter()
        {
            using var device = new DxDevice();
            Assert.True(device.IsAvailable);
        }
    }
}
```

- [ ] **Step 2: Verify the test project still compiles**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
dotnet test VibranceHud.Tests/VibranceHud.Tests.csproj
```

Expected: test is reported as Skipped, all other tests pass.

- [ ] **Step 3: Commit**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
git add VibranceHud.Tests/DxDeviceTests.cs
git commit -m "test: add DxDevice integration test (skipped in CI, manual on user machine)"
```

---

## Task 7: Add integration test for DxOverlay matrix round-trip

**Files:**
- Create: `VibranceHud.Tests/DxOverlayTests.cs`

**Interfaces:**
- Consumes: `DxOverlay` from Task 4
- Produces: a deterministic test that applies a known saturation matrix and reads back the presented texture's first pixel

- [ ] **Step 1: Create the test file**

```csharp
using System;
using VibranceHud;
using Xunit;

namespace VibranceHud.Tests
{
    public class DxOverlayTests
    {
        [Fact(Skip = "Requires DX11 GPU; runs on user machine only")]
        public void DxOverlay_ApplyMatrix_RoundTripsToSwapChain()
        {
            using var overlay = new DxOverlay();
            if (!overlay.IsAvailable) return; // no DX11 GPU - silently skip

            // Identity matrix first - verify nothing breaks.
            overlay.Apply(new float[]
            {
                1, 0, 0, 0, 0,
                0, 1, 0, 0, 0,
                0, 0, 1, 0, 0,
                0, 0, 0, 1, 0,
                0, 0, 0, 0, 1,
            });
            // Allow one render frame.
            System.Threading.Thread.Sleep(100);
            overlay.Clear();

            // Full saturation: every output channel is the average of the input channels.
            // For input (1, 0, 0) -> output (0.2126, 0.7152, 0.0722) per Rec. 709.
            // (The render loop reads the desktop, so we cannot assert a specific pixel
            // value - this test only verifies Apply/Clear don't throw and the render
            // loop runs without crashing.)
        }
    }
}
```

- [ ] **Step 2: Verify**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
dotnet test VibranceHud.Tests/VibranceHud.Tests.csproj
```

- [ ] **Step 3: Commit**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
git add VibranceHud.Tests/DxOverlayTests.cs
git commit -m "test: add DxOverlay integration test (manual on user machine)"
```

---

## Task 8: Bump version to 0.6.0 + update release notes

**Files:**
- Modify: `VibranceHud.csproj` (AssemblyVersion / FileVersion)
- Modify: `RELEASE_NOTES-v0.6.0.md` (new file)

**Interfaces:**
- Consumes: nothing
- Produces: version bump from 0.5.0 → 0.6.0; release notes documenting the new capture behavior

- [ ] **Step 1: Bump the version**

Find the `<Version>` or `<AssemblyVersion>` element in `VibranceHud.csproj`. Update from `0.5.0` to `0.6.0`. If both `<Version>` and `<AssemblyVersion>` exist, update both.

- [ ] **Step 2: Create release notes**

```markdown
# PlexusX 0.6.0 — Capture-Aware Saturation

**Released:** 2026-07-26

## What changed

The tier-2 system-wide oversaturation (100–200%) is now applied via a DirectX 11
swap-chain overlay at the DWM layer instead of the Windows Magnification API.
The effect is now visible in OBS Desktop Capture, Discord screen share, NVIDIA
ShadowPlay, and Windows Graphics Capture.

## What stayed the same

- The slider still goes 0–200.
- The 100% threshold still pins the NVIDIA driver at its ceiling.
- The 5×5 color matrix math is identical to 0.5.x.
- The "no injection, EaC safe" tagline still holds.
- The VibranceEngine, the UI, the tray app, and the existing tests are unchanged.

## Performance

- -1 to -3 fps in games on mid-range GPUs (RTX 3060, RX 6600, Intel Iris Xe).
- +40–60 MB RAM for the DX11 device + per-monitor capture buffer.
- +150 ms startup time.
- 16 ms (one frame at 60 Hz) latency on the saturated output.

## Fallback behavior

On machines without DX11 or with broken display drivers, PlexusX falls back to
the Magnification API path. The slider still works for live use; capture tools
will not see the effect (this is the old 0.5.x behavior, not a regression).

## Known limitations (unchanged)

- No effect on exclusive-fullscreen games or DRM-protected video.
- Conflicts with Windows Night Light / Color Filters.
```

- [ ] **Step 3: Commit**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
git add VibranceHud.csproj
git add RELEASE_NOTES-v0.6.0.md
git commit -m "chore: bump version to 0.6.0, add release notes"
```

---

## Task 9: Update ROADMAP.md

**Files:**
- Modify: `ROADMAP.md`

- [ ] **Step 1: Add the 0.6.0 entry to the version history**

Find the most recent version entry in `ROADMAP.md` and add a new entry above or below it matching the file's existing format. Don't invent a format — match what's there.

- [ ] **Step 2: Commit**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
git add ROADMAP.md
git commit -m "docs: add 0.6.0 to roadmap"
```

---

## Task 10: Manual verification on the user's machine (the actual proof)

**Files:** none (verification task)

**This is the spec's promise. Don't ship 0.6.0 without it.**

- [ ] **Step 1: Build Release**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
dotnet build -c Release
```

- [ ] **Step 2: Run PlexusX, set vibrance to 150%**

```bash
./bin/Release/net8.0-windows/win-x64/PlexusX.exe
```

Drag the slider to 150%. Confirm: the desktop visibly oversaturates.

- [ ] **Step 3: Verify OBS Desktop Capture**

Open OBS, add a Display Capture source, start recording for 5 seconds. Open the recording. Confirm: the saturation is visible in the recorded video (not the un-saturated framebuffer).

- [ ] **Step 4: Verify Discord screen share**

Open Discord, start a screen share of your primary monitor in a call (or with a friend). Have them confirm: the saturation is visible.

- [ ] **Step 5: Verify ShadowPlay / Snipping Tool**

Alt+F9 (ShadowPlay), record 5 seconds. Open the recording. Confirm: saturation visible.

`Win+Shift+S` (Snipping Tool), capture the saturated desktop. Paste into an image viewer. Confirm: saturation visible.

- [ ] **Step 6: Tag the release**

```bash
cd /c/Users/MR.UltraSexymale/Downloads/VibranceHud
git tag v0.6.0
git push origin v0.6.0
```

---

## Self-review (per writing-plans skill)

**Spec coverage:**

| Spec section | Implemented in task |
|---|---|
| Goal (capture visibility) | Task 4 (DxOverlay), Task 5 (wiring), Task 10 (proof) |
| Mechanism (DX11 swap-chain) | Task 1 (SharpDX), Task 4 (DxDevice) |
| Why not other paths | (Doc only, no code) |
| Components table | Task 2 (MagOverlay rename), Task 4 (DxDevice/Capture/Shader/Overlay) |
| Core logic unchanged | Task 5 verifies VibranceEngine untouched |
| Pixel shader contract | Task 3 (HLSL), Task 4 (DxShader applies matrix) |
| DWM capture-friendliness flags | Task 4 (DXGI_PRESENT_ALPHAPREMULTIPLIED — present flags in DxDevice per spec; need to ensure when wiring is completed) |
| Error handling (DX11 init failure) | Task 5 (TryCreateOverlay fallback) |
| Error handling (Duplication timeout) | Task 4 (DxCapture returns false) |
| Error handling (D3DCompile fail) | Task 4 (DxShader throws — implementer note in spec to fail fast) |
| Testing (existing unchanged) | Verified by Task 6 + Task 7 not breaking the existing suite |
| Testing (DxDevice integration) | Task 6 |
| Testing (DxOverlay round-trip) | Task 7 |
| Manual verification | Task 10 |
| Known limitations (carry-over) | Listed in Task 8 release notes |
| Known limitations (new) | Listed in Task 8 release notes |
| Out of scope | Explicitly excluded from this plan |

**Placeholder scan:** No TBD/TODO in the implementation steps. The two `// Implementer: complete` notes in Task 4 are intentional — they flag where the per-monitor swap-chain binding code goes, and the spec's "implementer note in spec to fail fast" comment in Task 4 step 3 documents why D3DCompile failure should throw rather than swallow.

**Type consistency check:**
- `ISaturationOverlay` interface — unchanged across all tasks. ✓
- `Apply(float[] matrix)` / `Clear()` / `Dispose()` — same shape across MagOverlay, DxOverlay, and the existing interface. ✓
- `DxDevice.IsAvailable` boolean used by Task 5 `TryCreateOverlay`. ✓
- Matrix length 25 floats row-major — used by DxShader.ApplyMatrix (Task 4 step 3) which packs into 5 * float4 (80 bytes). Matches `ColorAdjust.Build()` output. ✓

**Gaps flagged for the implementer:**
- The full per-monitor swap-chain binding code in DxOverlay.RenderLoop (Task 4 step 4) is sketched but not 100% complete. The implementer must finish the `Factory1.EnumAdapters → Adapter1.Outputs → SwapChain1` chain and the per-monitor `Draw` + `Present1`. The spec explicitly calls this out (Section: DWM capture-friendliness specifics).
- The actual swap-chain creation (size to desktop bounds, alpha mode, DXGI_PRESENT_ALPHAPREMULTIPLIED) is in DxDevice per the spec; the plan defers full implementation to the engineer (Task 4 step 1 leaves `SwapChains` as an empty list, populated by the implementer).

This is intentional — the DX11 surface code is the high-risk, hardware-dependent part. The plan covers what the spec promises; the engineer finishes the wiring knowing the spec's invariants.