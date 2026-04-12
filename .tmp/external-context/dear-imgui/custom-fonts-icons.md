---
source: Official GitHub raw (FONTS.md)
library: Dear ImGui
package: dear-imgui
topic: Custom fonts binary compressed icon hashes FA Ubuntu
fetched: 2026-04-11T12:00:00Z
official_docs: https://github.com/ocornut/imgui/blob/master/docs/FONTS.md
---

## Loading Binary Compressed Fonts
Compile misc/fonts/binary_to_compressed_c.cpp to embed TTF as C array.

```cpp
ImFont* font = io.Fonts->AddFontFromMemoryCompressedTTF(compressed_data, compressed_data_size, size_pixels);
```

Keep data until atlas build.

## Icon Fonts (FontAwesome / Ubuntu Mono)
Merge icons into main font:

```cpp
io.Fonts->AddFontDefaultVector(); // or AddFontFromFileTTF
ImFontConfig config;
config.MergeMode = true;
config.GlyphMinAdvanceX = 13.0f; // Monospace icons
static const ImWchar icon_ranges[] = { ICON_MIN_FA, ICON_MAX_FA, 0 };
io.Fonts->AddFontFromFileTTF("fontawesome-webfont.ttf", 13.0f, &config, icon_ranges);
```

For Ubuntu font (mono): AddFontFromFileTTF("UbuntuMono-R.ttf", size_pixels);

Icon hashes: Use IconFontCppHeaders for codepoints e.g. ICON_FA_SEARCH as UTF-8 string.

Usage:
```cpp
ImGui::Text("%s Search %d", ICON_FA_SEARCH, count);
ImGui::Button(ICON_FA_SEARCH " Search");
```

## DPI Handling
style.ScaleAllSizes(dpi_scale);
style.FontScaleDpi = dpi_scale;