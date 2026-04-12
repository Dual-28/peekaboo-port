---
source: NuGet.org + Official docs
library: ImGui.NET
package: imgui-net
topic: dependencies
fetched: 2026-04-11T12:00:00Z
official_docs: https://www.nuget.org/packages/ImGui.NET https://github.com/ImGuiNET/ImGui.NET
---

## Core Dependencies

ImGui.NET depends on:
- System.Buffers (&gt;= 4.5.1)
- System.Numerics.Vectors (&gt;= 4.5.0)
- System.Runtime.CompilerServices.Unsafe (&gt;= 6.0.0)

Native dependency: cimgui (bundled).

## Backend Dependencies (Silk.NET OpenGL/GLFW)

For OpenGL backend with Silk.NET, use `Silk.NET.OpenGL.Extensions.ImGui` which requires:
- ImGui.NET (&gt;= 1.90.8.1)
- Silk.NET.OpenGL (&gt;= 2.23.0)
- Silk.NET.Windowing.Common (&gt;= 2.23.0)
- Silk.NET.Input.Common (&gt;= 2.23.0)
- Silk.NET.Input.Extensions (&gt;= 2.23.0)

Install via:
```
dotnet add package Silk.NET.OpenGL.Extensions.ImGui
```

For GLFW windowing: Add `Silk.NET.GLFW` if using GLFW backend.

For Win32 transparent overlay: `ClickableTransparentOverlay` (uses DX11 backend, Vortice.Direct3D11, etc.).