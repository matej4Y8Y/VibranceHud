# Third-party notices

PlexusX ships with the components below. Each is used under its own licence, and
nothing in `LICENSE` limits the rights those licences grant you.

Generated against the package versions referenced in `VibranceHud.csproj`. A test
(`ThirdPartyNoticesTests`) fails the build if a package is added to the project and
not listed here, so this file cannot silently fall behind.

---

## NvAPIWrapper.Net 0.8.1.101

Managed wrapper around NVIDIA's NVAPI. PlexusX uses it to read and set the driver's
digital vibrance level on NVIDIA GPUs.

- Project: https://github.com/falahati/NvAPIWrapper
- Licence: LGPL-3.0 — https://github.com/falahati/NvAPIWrapper/blob/master/LICENSE

Under the LGPL, you are entitled to the source of this library, and to replace it
with a modified version. PlexusX consumes it as an unmodified NuGet package and links
against it dynamically; no changes have been made to it. The published source at the
project URL above is the version in use.

## SharpDX 4.2.0

And its components, all under the same licence and the same copyright:

- SharpDX 4.2.0
- SharpDX.Direct3D11 4.2.0
- SharpDX.DXGI 4.2.0
- SharpDX.D3DCompiler 4.2.0

Managed DirectX bindings. Used by the DX11 overlay path and for enumerating display
adapters and outputs.

- Project: http://sharpdx.org/
- Licence: MIT — http://sharpdx.org/License.txt

> Copyright (c) 2010-2019 Alexandre Mutel
>
> Permission is hereby granted, free of charge, to any person obtaining a copy of
> this software and associated documentation files (the "Software"), to deal in the
> Software without restriction, including without limitation the rights to use, copy,
> modify, merge, publish, distribute, sublicense, and/or sell copies of the Software,
> and to permit persons to whom the Software is furnished to do so, subject to the
> following conditions:
>
> The above copyright notice and this permission notice shall be included in all
> copies or substantial portions of the Software.
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
> INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
> PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
> HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
> OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
> SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## System.Management 8.0.0

Microsoft's WMI access library. Used once, to read the CPU and disk identifiers that
form the licence's machine fingerprint.

- Project: https://dot.net/
- Licence: MIT — https://licenses.nuget.org/MIT

> Copyright (c) .NET Foundation and Contributors
>
> Permission is hereby granted, free of charge, to any person obtaining a copy of
> this software and associated documentation files (the "Software"), to deal in the
> Software without restriction, including without limitation the rights to use, copy,
> modify, merge, publish, distribute, sublicense, and/or sell copies of the Software,
> and to permit persons to whom the Software is furnished to do so, subject to the
> following conditions:
>
> The above copyright notice and this permission notice shall be included in all
> copies or substantial portions of the Software.
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
> INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
> PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
> HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
> OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
> SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## .NET 8 runtime

PlexusX is published self-contained, so a copy of the .NET runtime is distributed with
it under the MIT licence.

- Project: https://github.com/dotnet/runtime
- Licence: MIT — https://github.com/dotnet/runtime/blob/main/LICENSE.TXT
