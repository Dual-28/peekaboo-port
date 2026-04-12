using System;
using System.Numerics;
using ImGuiNET;

namespace Peekaboo.Platform.Windows.Gui;

public class ImGuiManager
{
    public bool Initialized { get; private set; }

    public void Initialize()
    {
        ImGui.CreateContext();
        var io = ImGui.GetIO();

        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;

        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors;
        io.BackendFlags |= ImGuiBackendFlags.HasSetMousePos;

        ImGui.StyleColorsDark();
        
        var style = ImGui.GetStyle();
        style.WindowRounding = 4.0f;
        style.FrameRounding = 4.0f;
        style.PopupRounding = 4.0f;
        style.ScrollbarRounding = 9.0f;
        style.GrabRounding = 3.0f;
        style.TabRounding = 4.0f;
        style.WindowTitleAlign = new Vector2(0.5f, 0.5f);

        style.Colors[(int)ImGuiCol.WindowBg] = new Vector4(0.06f, 0.06f, 0.06f, 0.94f);
        style.Colors[(int)ImGuiCol.TitleBg] = new Vector4(0.04f, 0.04f, 0.04f, 1.00f);
        style.Colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.16f, 0.29f, 0.48f, 1.00f);
        style.Colors[(int)ImGuiCol.Tab] = new Vector4(0.28f, 0.28f, 0.30f, 0.86f);
        style.Colors[(int)ImGuiCol.TabSelected] = new Vector4(0.38f, 0.38f, 0.40f, 1.00f);
        style.Colors[(int)ImGuiCol.Text] = new Vector4(0.83f, 0.83f, 0.83f, 1.00f);
        style.Colors[(int)ImGuiCol.Button] = new Vector4(0.26f, 0.26f, 0.30f, 1.00f);
        style.Colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.35f, 0.35f, 0.40f, 1.00f);
        style.Colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.20f, 0.20f, 0.25f, 1.00f);
        style.Colors[(int)ImGuiCol.FrameBg] = new Vector4(0.14f, 0.14f, 0.16f, 1.00f);
        style.Colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.20f, 0.20f, 0.23f, 1.00f);
        style.Colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.08f, 0.08f, 0.10f, 1.00f);
        style.Colors[(int)ImGuiCol.ScrollbarGrab] = new Vector4(0.25f, 0.25f, 0.30f, 1.00f);
        style.Colors[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(0.35f, 0.35f, 0.40f, 1.00f);
        style.Colors[(int)ImGuiCol.ScrollbarGrabActive] = new Vector4(0.20f, 0.20f, 0.25f, 1.00f);
        style.Colors[(int)ImGuiCol.Border] = new Vector4(0.15f, 0.15f, 0.18f, 1.00f);
        style.Colors[(int)ImGuiCol.Separator] = new Vector4(0.20f, 0.20f, 0.23f, 1.00f);
        style.Colors[(int)ImGuiCol.ChildBg] = new Vector4(0.10f, 0.10f, 0.12f, 1.00f);
        style.Colors[(int)ImGuiCol.TextSelectedBg] = new Vector4(0.20f, 0.20f, 0.25f, 1.00f);

        Initialized = true;
    }

    public void NewFrame()
    {
        if (!Initialized) return;
        ImGui.NewFrame();
    }

    public void Render()
    {
        if (!Initialized) return;
        ImGui.Render();
    }

    public void Shutdown()
    {
        if (Initialized)
        {
            ImGui.DestroyContext(ImGui.GetCurrentContext());
            Initialized = false;
        }
    }
}
