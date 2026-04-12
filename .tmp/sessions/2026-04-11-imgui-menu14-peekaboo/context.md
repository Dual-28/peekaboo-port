# Task Context: ImGui Menu 14 UI for Peekaboo Port

Session ID: 2026-04-11-imgui-menu14-peekaboo
Created: 2026-04-11T03:40:00Z
Status: in_progress

## Current Request
analyze C:\\Users\\dualtach\\Documents\\AASOLITUDE\\Peekaboo-main\\ImGuiMenu-s\\14 i want to use this type of ui for my peekaboo port

## Context Files (Standards to Follow)
- (none discovered by ContextScout)

## Reference Files (Source Material to Look At)
- ImGuiMenu-s/14/** (C++ DX9 example: main.cpp, customgui.cpp/h, fonts/*.hpp, imgui_rotate.hpp, hashes.h)
- Peekaboo-Windows/**/*.csproj
- Peekaboo-Windows/Peekaboo.Platform.Windows/**/*.cs (services: ScreenCapture, WindowMgmt, Input, etc.)
- Peekaboo-Windows/Peekaboo.Core/**/*.cs (interfaces)

## External Docs Fetched
- .tmp/external-context/dear-imgui/win32-dx9-backend-setup.md
- .tmp/external-context/dear-imgui/custom-fonts-icons.md
- .tmp/external-context/dear-imgui/custom-widgets-styling.md
- .tmp/external-context/imgui-net/csharp-bindings.md

## Components
- ImGui core + backend setup (prefer C# ImGui.NET + DX/OpenGL)
- Custom fonts (Ubuntu/FA Pro compressed), icon hashes
- Menu rendering (tabs, panels, custom widgets from example 14)
- Integration: Bind Peekaboo services to ImGui controls

## Constraints
- Project lang: C# .NET 10 (no C++ sources)
- Example 14: C++ Win32+DX9 → Translate to C#/ImGui.NET + compatible renderer
- Overlay/UI for desktop automation (screen/window/input services)
- Win32 platform

## Exit Criteria
- [ ] Standalone ImGui demo matching 14 style (fonts/layout/custom)
- [ ] Integrated into Peekaboo: Services exposed/configurable via UI
- [ ] Builds/runs without errors
- [ ] Basic tests: UI interaction updates services