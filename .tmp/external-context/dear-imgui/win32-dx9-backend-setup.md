---
source: Official GitHub raw (BACKENDS.md + example_win32_directx9/main.cpp)
library: Dear ImGui
package: dear-imgui
topic: Win32 + DirectX9 backend setup installation config examples
fetched: 2026-04-11T12:00:00Z
official_docs: https://github.com/ocornut/imgui/blob/master/docs/BACKENDS.md
---

## Backend Files
- Platform: backends/imgui_impl_win32.cpp, imgui_impl_win32.h
- Renderer: backends/imgui_impl_dx9.cpp, imgui_impl_dx9.h

Combine one Platform + one Renderer backend.

Example application: examples/example_win32_directx9/

## Key Setup Steps
1. Call ImGui_ImplWin32_EnableDpiAwareness() for DPI awareness.
2. Create Win32 window (WNDCLASSEX, CreateWindow).
3. Initialize D3D9: Direct3DCreate9, CreateDevice.
4. ImGui::CreateContext();
5. ImGuiIO& io = ImGui::GetIO(); io.ConfigFlags |= ImGuiConfigFlags_NavEnableKeyboard;
6. ImGui_ImplWin32_Init(hwnd);
7. ImGui_ImplDX9_Init(g_pd3dDevice);
8. Load fonts if needed: io.Fonts->AddFontDefault();
9. Main loop:
   - ImGui_ImplDX9_NewFrame();
   - ImGui_ImplWin32_NewFrame();
   - ImGui::NewFrame();
   - Your ImGui code...
   - ImGui::Render();
   - g_pd3dDevice->SetRenderState(...); Clear();
   - ImGui_ImplDX9_RenderDrawData(ImGui::GetDrawData());
   - Present();
10. Cleanup: ImGui_ImplDX9_Shutdown(); ImGui_ImplWin32_Shutdown(); ImGui::DestroyContext();

## Full Example Code (main.cpp)
```cpp
// (full code from fetched, abbreviated for brevity)
#include "imgui.h"
#include "imgui_impl_dx9.h"
#include "imgui_impl_win32.h"
#include <d3d9.h>
// ... CreateDeviceD3D, WndProc, main loop as above
```
(Full implementation details in example_win32_directx9/main.cpp)