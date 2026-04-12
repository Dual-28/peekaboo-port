---
source: Official GitHub README
library: ImGui.NET
package: imgui.net
topic: overview-csharp-api-usage-context-creation
fetched: 2026-04-11T12:00:00Z
official_docs: https://github.com/ImGuiNET/ImGui.NET
---

# ImGui.NET

This is a .NET wrapper for the immediate mode GUI library, Dear ImGui. ImGui.NET lets you build graphical interfaces using a simple immediate-mode style. ImGui.NET is a .NET Standard library, and can be used on all major .NET runtimes and operating systems.

## Usage

ImGui.NET currently provides a raw wrapper around the ImGui native API, and also provides a very thin safe, managed API for convenience. It is currently very much like using the native library, which is very simple, flexible, and robust. The easiest way to figure out how to use the library is to read the documentation of imgui itself, mostly in the imgui.cpp, and imgui.h files, as well as the exported functions in cimgui.h. Looking at the sample program code will also give some indication about basic usage.

Typical context creation:
```
ImGuiContext* ctx = ImGui.CreateContext();
ImGui.SetCurrentContext(ctx);
```
