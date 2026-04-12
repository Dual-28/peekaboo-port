---
source: Official GitHub + NuGet.org (Silk.NET extension)
library: ImGui.NET
package: imgui-net
topic: silk-net-opengl-glfw-backend
fetched: 2026-04-11T12:00:00Z
official_docs: https://github.com/ImGuiNET/ImGui.NET https://www.nuget.org/packages/Silk.NET.OpenGL.Extensions.ImGui https://github.com/dotnet/Silk.NET
---

## Silk.NET Packages for OpenGL/GLFW Backend

Use the `Silk.NET.OpenGL.Extensions.ImGui` extension package for easy ImGui integration with Silk.NET OpenGL.

**Installation:**
```
dotnet add package Silk.NET.OpenGL.Extensions.ImGui --version 2.23.0
```

**Required Packages (transitive):**
- Silk.NET.OpenGL
- Silk.NET.Windowing.Common
- Silk.NET.Input.Common
- Silk.NET.Input.Extensions

**For GLFW Backend:**
Add `Silk.NET.GLFW` for GLFW window creation:
```
dotnet add package Silk.NET.GLFW
```

**Win32 Desktop Overlay Context:**
- Silk.NET provides cross-platform windowing.
- For transparent overlays on Windows, consider `ClickableTransparentOverlay` library (DX11-based, but adaptable).
- See ImGui.NET examples/Silk.NETExample for setup (renderer + input handling).

**Common Patterns:**
ImGui outputs vertex buffers; render with OpenGL context from Silk.NET.
Follow Dear ImGui backend patterns (imgui_impl_opengl3, imgui_impl_glfw).