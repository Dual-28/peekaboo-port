---
source: Official GitHub raw (ImGui.NET README.md + wiki)
library: ImGui.NET
package: imgui-net
topic: C# bindings ImGui.NET backends installation config
fetched: 2026-04-11T12:00:00Z
official_docs: https://github.com/ImGuiNET/ImGui.NET
---

## Installation
- NuGet restore packages.
- Build with VS2015+ (x64 Windows native).
- Backends in backends/ as .cs (imgui_impl_win32.cs, imgui_impl_dx9.cs etc.).

## Usage
Thin wrapper over cimgui C API.

```csharp
// Similar to C++: CreateContext, backend Init/NewFrame/Render, etc.
ImGui.CreateContext();
ImGui_ImplWin32_Init(hwnd);
ImGui_ImplDX9_Init(device);
// Loop: NewFrame, your UI, Render, RenderDrawData
```

Sample program included.

For games/overlays: Port backends to C#, hook DX9 Present/EndScene, init context there.

NuGet: ImGui.NET, OpenTK for GL sample.