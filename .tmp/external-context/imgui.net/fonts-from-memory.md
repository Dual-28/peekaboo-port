---
source: Official ImGui docs (FONTS.md)
library: ImGui.NET
package: imgui.net
topic: custom-fonts-from-memory-ttf-compressed-glyph-ranges-merge
fetched: 2026-04-11T03:20:00Z
official_docs: https://github.com/ocornut/imgui/blob/master/docs/FONTS.md
tech_stack: C# .NET ImGui app (ImGuiManager.cs)
---

## Loading Font Data from Memory (ImGui.NET C# compatible)

```csharp
ImFont* font = io.Fonts->AddFontFromMemoryTTF(data, data_size, size_pixels, ...);
```

**In ImGui.NET C#:**
```csharp
var font = io.Fonts.AddFontFromMemoryTTF(fontData, (int)fontData.Length, size_pixels, font_cfg, glyph_ranges);
```

**Note:** `AddFontFromMemoryTTF()` transfers ownership of the data buffer to the font atlas by default. To keep ownership:
```csharp
var font_cfg = new ImFontConfig();
font_cfg.FontDataOwnedByAtlas = false;
var font = io.Fonts.AddFontFromMemoryTTF(fontData, (int)fontData.Length, size_pixels, font_cfg);
```

## Loading Compressed Font Data Embedded In Source Code

Use `misc/fonts/binary_to_compressed_c.cpp` to create compressed C array from .ttf file.

Load with:
```cpp
ImFont* font = io.Fonts->AddFontFromMemoryCompressedTTF(compressed_data, compressed_data_size, size_pixels, ...);
```
or Base85:
```cpp
ImFont* font = io.Fonts->AddFontFromMemoryCompressedBase85TTF(compressed_data_base85, size_pixels, ...);
```

**C# byte[] from C++ uint8_t[]:** Copy the uint8_t array values as `byte[] data = { 0xAB, 0xCD, ... };`

## Using Icon Fonts (FontAwesome example)

Merge icons into main font:
```cpp
ImFontConfig config;
config.MergeMode = true;
config.GlyphMinAdvanceX = 13.0f; // Monospace icons
static const ImWchar icon_ranges[] = { ICON_MIN_FA, ICON_MAX_FA, 0 };
io.Fonts->AddFontFromFileTTF("fontawesome-webfont.ttf", 13.0f, &config, icon_ranges);
```

**Exact C# match to query:**
```csharp
ImFontConfig config = new() { MergeMode = true };
ImWchar[] icon_ranges = new ImWchar[] { new ImWchar(ICON_MIN_FA), new ImWchar(ICON_MAX_FA), new ImWchar(0) };
io.Fonts.AddFontFromMemoryTTF(faData, 13.0f, config, icon_ranges);
```

Usage:
```cpp
ImGui::Text("%s among %d items", ICON_FA_SEARCH, count);
ImGui::Button(ICON_FA_SEARCH " Search");
```

**In demo:** Use `ImGui::PushFont(my_icon_font); ... ImGui::PopFont();`

## Security: Validate Font Data

- Check TTF header: first 5 bytes should be `0x00, 0x01, 0x00, 0x00, 0x00`
- Invalid data triggers asserts/crashes.
- For user-supplied fonts: validate size, magic bytes before loading.

## Context: ImGuiManager.cs before Build()

Load fonts in initialization, before `io.Fonts.Build()` (called by backend NewFrame()).

Common pitfalls:
- Forget `MergeMode = true` for icons.
- Glyph ranges must persist until Build().
- Load before atlas build.
- Ownership of data buffer.

---
*API identical in ImGui.NET bindings. Use byte[] for data. See ImGui.NET GitHub for NuGet.*