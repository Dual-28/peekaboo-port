using System;
using System.Runtime.InteropServices;
using System.Numerics;
using ImGuiNET;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Silk.NET.Input;

namespace Peekaboo.Gui.Wpf.Rendering;

public static class ImGuiInterop
{
    private static Glfw? _glfw;
    private static IWindow? _window;
    private static GL? _gl;
    private static bool _initialized;

    private static int _shaderProgram;
    private static int _vbo;
    private static int _ebo;
    private static int _vao;
    private static uint _textureId;

    private const string VertexShaderSource = @"#version 330 core
layout (location = 0) in vec2 Position;
layout (location = 1) in vec2 UV;
layout (location = 2) in vec4 Color;
uniform mat4 ProjMtx;
out vec2 Frag_UV;
out vec4 Frag_Color;
void main() {
    Frag_UV = UV;
    Frag_Color = Color;
    gl_Position = ProjMtx * vec4(Position, 0.0, 1.0);
}";

    private const string FragmentShaderSource = @"#version 330 core
in vec2 Frag_UV;
in vec4 Frag_Color;
uniform sampler2D Texture;
out vec4 Out_Color;
void main() {
    Out_Color = Frag_Color * texture(Texture, Frag_UV.st);
}";

    public static unsafe bool Init(IWindow window, GL gl)
    {
        if (_initialized) return true;

        try
        {
            _window = window;
            _gl = gl;
            _glfw = Glfw.GetApi();

            var io = ImGui.GetIO();
            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
            io.BackendFlags |= ImGuiBackendFlags.PlatformHasViewports;
            io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors;
            io.BackendFlags |= ImGuiBackendFlags.HasSetMousePos;

            io.SetClipboardTextFn = SetClipboardText;
            io.GetClipboardTextFn = GetClipboardText;
            io.ClipboardUserData = IntPtr.Zero;

            CreateDeviceObjects();

            _initialized = true;
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ImGuiInterop.Init error: {ex.Message}");
            return false;
        }
    }

    private static unsafe void CreateDeviceObjects()
    {
        if (_gl == null) return;

        var io = ImGui.GetIO();

        byte[] pixels;
        int width, height;
        ImGuiNative.ImFontAtlas_GetTexDataAsRGBA32(io.Fonts.NativePtr, out pixels, out width, out height, null);

        _textureId = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _textureId);
        
        fixed (byte* pixelsPtr = pixels)
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixelsPtr);
        }
        
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        io.Fonts.SetTexID(new IntPtr(_textureId));
        io.Fonts.ClearTexData();

        _shaderProgram = CreateShader();

        _vbo = (int)_gl.GenBuffer();
        _ebo = (int)_gl.GenBuffer();
        _vao = (int)_gl.GenVertexArray();

        _gl.BindVertexArray((uint)_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, (uint)_vbo);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, VertexAttribType.Float, false, (uint)sizeof(ImDrawVert), (void*)0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 2, VertexAttribType.Float, false, (uint)sizeof(ImDrawVert), (void*)(2 * sizeof(float)));
        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribPointer(2, 4, VertexAttribType.UnsignedByte, true, (uint)sizeof(ImDrawVert), (void*)(4 * sizeof(float)));

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, (uint)_ebo);
        _gl.BindVertexArray(0);
    }

    private static int CreateShader()
    {
        if (_gl == null) return 0;

        var vertShader = _gl.CreateShader(ShaderType.VertexShader);
        _gl.ShaderSource(vertShader, VertexShaderSource);
        _gl.CompileShader(vertShader);

        var fragShader = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(fragShader, FragmentShaderSource);
        _gl.CompileShader(fragShader);

        var program = _gl.CreateProgram();
        _gl.AttachShader(program, vertShader);
        _gl.AttachShader(program, fragShader);
        _gl.LinkProgram(program);

        _gl.DeleteShader(vertShader);
        _gl.DeleteShader(fragShader);

        return (int)program;
    }

    public static void NewFrame(IWindow window)
    {
        var io = ImGui.GetIO();

        var size = window.Size;
        io.DisplaySize = new Vector2(size.X, size.Y);
        io.DisplayFramebufferScale = new Vector2(1, 1);

        io.DeltaTime = 1f / 60f;

        var mousePos = window.MousePosition;
        io.MousePos = new Vector2(mousePos.X, mousePos.Y);

        if (window.Mouse != null)
        {
            for (int i = 0; i < 3; i++)
            {
                io.MouseDown[i] = window.Mouse.IsButtonPressed((MouseButton)i);
            }
        }

        ImGui.NewFrame();
    }

    public static unsafe void Render(GL gl)
    {
        var drawData = ImGui.GetDrawData();
        if (drawData.CmdListsCount == 0) return;

        var io = ImGui.GetIO();

        float scaleX = 2f / io.DisplaySize.X;
        float scaleY = -2f / io.DisplaySize.Y;

        float[] projMtx = new float[]
        {
            scaleX, 0f, 0f, 0f,
            0f, scaleY, 0f, 0f,
            0f, 0f, -1f, 0f,
            -1f, 1f, 0f, 1f
        };

        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorFunc.OneMinusSrcAlpha);
        gl.Disable(EnableCap.CullFace);
        gl.Disable(EnableCap.DepthTest);
        gl.Enable(EnableCap.ScissorTest);

        gl.UseProgram((uint)_shaderProgram);
        int loc = gl.GetUniformLocation((uint)_shaderProgram, "ProjMtx");
        gl.Uniform1(loc, projMtx);

        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _textureId);

        gl.BindVertexArray((uint)_vao);

        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmdList = drawData.GetCmdList(n);
            if (cmdList == null) continue;

            var vtxBuffer = cmdList.VtxBuffer;
            var idxBuffer = cmdList.IdxBuffer;

            gl.BindBuffer(BufferTargetARB.ArrayBuffer, (uint)_vbo);
            fixed (ImDrawVert* vtxPtr = vtxBuffer.Data)
            {
                gl.BufferData(BufferTargetARB.ArrayBuffer, vtxBuffer.Size * sizeof(ImDrawVert), vtxPtr, BufferUsageARB.DynamicDraw);
            }

            gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, (uint)_ebo);
            fixed (ushort* idxPtr = idxBuffer.Data)
            {
                gl.BufferData(BufferTargetARB.ElementArrayBuffer, idxBuffer.Size * sizeof(ushort), idxPtr, BufferUsageARB.DynamicDraw);
            }

            for (int cmdIdx = 0; cmdIdx < cmdList.CmdBuffer.Size; cmdIdx++)
            {
                var cmd = cmdList.GetCmdBuffer<ImDrawCmd>(cmdIdx);

                if (cmd.UserCallback != IntPtr.Zero) continue;

                float left = drawData.DisplayPos.X + cmd.ClipRect.X;
                float top = drawData.DisplayPos.Y + cmd.ClipRect.Y;
                float right = drawData.DisplayPos.X + cmd.ClipRect.Z;
                float bottom = drawData.DisplayPos.Y + cmd.ClipRect.W;

                gl.Scissor((int)left, (int)(io.DisplaySize.Y - bottom), (int)(right - left), (int)(bottom - top));

                if (cmd.TextureId != IntPtr.Zero)
                {
                    gl.BindTexture(TextureTarget.Texture2D, (uint)cmd.TextureId.ToPointer());
                }

                gl.DrawElementsBaseVertex(PrimitiveType.Triangles, (uint)cmd.ElemCount, DrawElementsType.UnsignedShort, (void*)(cmd.IdxOffset * sizeof(ushort)), (int)cmd.VtxOffset);
            }
        }

        gl.BindVertexArray(0);
        gl.UseProgram(0);
        gl.Disable(EnableCap.ScissorTest);
    }

    public static void Shutdown()
    {
        if (!_initialized) return;

        if (_gl != null)
        {
            if (_vbo > 0) _gl.DeleteBuffer((uint)_vbo);
            if (_ebo > 0) _gl.DeleteBuffer((uint)_ebo);
            if (_vao > 0) _gl.DeleteVertexArray((uint)_vao);
            if (_textureId > 0) _gl.DeleteTexture(_textureId);
            if (_shaderProgram > 0) _gl.DeleteProgram((uint)_shaderProgram);
        }

        _vbo = 0;
        _ebo = 0;
        _vao = 0;
        _textureId = 0;
        _shaderProgram = 0;

        _initialized = false;
    }

    private static string GetClipboardText(IntPtr user_data)
    {
        return _glfw?.GetClipboardString(null) ?? "";
    }

    private static void SetClipboardText(IntPtr user_data, string text)
    {
        _glfw?.SetClipboardString(null, text);
    }
}