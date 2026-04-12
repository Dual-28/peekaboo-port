---
source: Dear ImGui demo code (C++ API identical in C#)
library: ImGui.NET
package: imgui.net
topic: style-customization-dark-theme-tabs-fonts-demo-example
fetched: 2026-04-11T12:00:00Z
official_docs: https://github.com/ocornut/imgui
---

Dear ImGui demo contains style editor with tabs and fonts preview (example ~14).

Dark theme:
```
ImGui.StyleColorsDark();
```

Style editor demo code excerpt (tabs, colors, fonts preview):
```
// From imgui_demo.cpp ShowStyleEditor()
if (ImGui::BeginTabBar("##style"))
{
    if (ImGui::BeginTabItem("Colors"))
    {
        // ... color editing
    }
    if (ImGui::BeginTabItem("Fonts"))
    {
        // Fonts preview and loading
        ImGui::PushFont(ImGui::GetIO().Fonts->Fonts[0]);
        // ...
    }
    ImGui::EndTabBar();
}
```
Full demo provides basic window examples, input handling.

For security: Use InputText flags like ImGuiInputTextFlags_CallbackCharFilter for input validation.
