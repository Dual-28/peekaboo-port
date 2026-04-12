---
source: Official GitHub README + NuGet.org
library: ImGui.NET
package: imgui-net
topic: installation-instructions
fetched: 2026-04-11T12:00:00Z
official_docs: https://github.com/ImGuiNET/ImGui.NET https://www.nuget.org/packages/ImGui.NET
---

## Installation Instructions

Install the ImGui.NET NuGet package:

### .NET CLI
```
dotnet add package ImGui.NET
```
or specific version:
```
dotnet add package ImGui.NET --version 1.91.6.1
```

### Package Manager Console (Visual Studio)
```
Install-Package ImGui.NET -Version 1.91.6.1
```

### PackageReference
```
&lt;PackageReference Include=&quot;ImGui.NET&quot; Version=&quot;1.91.6.1&quot; /&gt;
```

The package includes pre-built native libraries (cimgui) for Windows, macOS, and mainline Linux distributions. For other platforms, build cimgui from source.