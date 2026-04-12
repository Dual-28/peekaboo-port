---
source: Official GitHub and NuGet.org
library: Silk.NET
package: silk.net.*
topic: ImGui.NET backend packages (OpenGL, Windowing, Input, GLFW) installation version compatibility Windows C#
fetched: 2026-04-11T03:15:00Z
official_docs: https://github.com/dotnet/Silk.NET
---

# Silk.NET Packages for ImGui.NET Backend (Windows C#)

## Latest Version
2.23.0 (released Jan 23, 2026 - Winter 2025 Update)

## Required Packages
- **Silk.NET.Windowing** - Cross-platform windowing (GLFW backend for Windows)
- **Silk.NET.OpenGL** - OpenGL bindings for rendering ImGui
- **Silk.NET.Input** - Input handling (keyboard, mouse, gamepad)

Optional for direct GLFW access:
- **Silk.NET.GLFW**

## Installation Commands
```
dotnet add package Silk.NET.Windowing --version 2.23.0
dotnet add package Silk.NET.OpenGL --version 2.23.0
dotnet add package Silk.NET.Input --version 2.23.0
```

## Key Dependencies (auto-installed)
- Silk.NET.Core
- Silk.NET.Maths
- Silk.NET.Windowing.Glfw (GLFW backend)
- Silk.NET.Input.Glfw
- Ultz.Bcl.Half (half-precision floats)

## Version Compatibility
- All Silk.NET packages are fully compatible when using the **same version** (e.g., all 2.23.0).
- Supports .NET Standard 2.0+, .NET 6+, Windows desktop.
- GLFW backend works out-of-the-box on Windows.

## Usage Notes for ImGui.NET
- Create GLFW window with Silk.NET.Windowing.
- Get OpenGL context from window.
- Use Silk.NET.Input for ImGui input polling.
- Implement ImGui OpenGL renderer backend.
- Examples: https://github.com/dotnet/Silk.NET/tree/main/examples/CSharp
- Community support: https://discord.gg/DTHHXRt

Silk.NET 3.0 in development for future improvements.