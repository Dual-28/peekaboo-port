---
source: ImGui demo.cpp + FONTS.md
library: ImGui.NET
package: imgui.net
topic: pushfont-usage-demo
fetched: 2026-04-11T03:20:00Z
official_docs: https://github.com/ocornut/imgui/blob/master/imgui_demo.cpp
---

## PushFont Usage in Demo

From imgui_demo.cpp:
```cpp
ImGui::PushFont(my_icon_font);
ImGui::Text("%s Hello", ICON_FA_SEARCH);
ImGui::PopFont();
```

Apply to style/colors as needed.

---
