---
source: Official GitHub raw (imgui_demo.cpp + FONTS.md + BACKENDS.md)
library: Dear ImGui
package: dear-imgui
topic: Custom widgets styling rotate customgui tabs panels fonts examples menu style
fetched: 2026-04-11T12:00:00Z
official_docs: https://github.com/ocornut/imgui/blob/master/imgui_demo.cpp
---

## Styling
```cpp
ImGuiStyle& style = ImGui::GetStyle();
style.Colors[ImGuiCol_WindowBg] = ImVec4(0.1f, 0.1f, 0.1f, 1.0f); // Dark
style.WindowRounding = 5.0f;
```

PushStyle:
```cpp
ImGui::PushStyleVar(ImGuiStyleVar_WindowRounding, 10.0f);
ImGui::Begin("Rounded");
ImGui::PopStyleVar();
```

## Custom Widgets / Rotate
Use ImDrawList for custom drawing:
```cpp
ImDrawList* draw_list = ImGui::GetWindowDrawList();
draw_list->AddRectFilled(...);
draw_list->AddCircle(...);
draw_list->AddImageRotated(user_texture_id, center, size, rot, pivot, col);
```

## Tabs Panels (menu style)
```cpp
if (ImGui::BeginTabBar("MyTabBar"))
{
    if (ImGui::BeginTabItem("Tab 1"))
    {
        ImGui::BeginChild("Panel1", size);
        // Content
        ImGui::EndChild();
        ImGui::EndTabItem();
    }
    ImGui::EndTabBar();
}
```

Fonts: PushFont(font, size); ... PopFont();

Dark theme panels/tabs as in demo Metrics/Debugger windows.