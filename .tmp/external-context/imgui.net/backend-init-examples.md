---
source: Official GitHub + cached Silk.NET
library: ImGui.NET
package: imgui.net
topic: backend-initialization-win32-opengl-silknet-wpf-glcontrol
fetched: 2026-04-11T12:00:00Z
official_docs: https://github.com/ImGuiNET/ImGui.NET
---

Backend initialization example (Win32/OpenGL):
```
// Win32 backend
ImGui_ImplWin32_Init(hwnd);

// OpenGL backend (Silk.NET provides GL context)
ImGui_ImplOpenGL3_Init(glsl_version = "#version 460");

// In render loop
ImGui_ImplOpenGL3_NewFrame();
ImGui_ImplWin32_NewFrame();
ImGui.NewFrame();
// UI code
ImGui.Render();
ImGui_ImplOpenGL3_RenderDrawData(ImGui.GetDrawData());
```

For Silk.NET: Use Silk.NET.Windowing for window/GL context, then above.

WPF integration: Host Silk.NET GLControl or OpenTK.GLControl in WPF window, attach ImGui backends.

See cached .tmp/external-context/silk.net/imgui-backend-windows.md for Silk.NET specific backend details.

Basic demo window:
```
ImGui.Begin("Demo Window");
ImGui.Text("Hello, ImGui.NET!");
if (ImGui.Button("Button"))
    counter++;
ImGui.End();
```
