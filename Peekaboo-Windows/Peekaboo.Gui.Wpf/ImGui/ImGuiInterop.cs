using System;
using System.Runtime.InteropServices;
using System.Numerics;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Peekaboo.Gui.Wpf.Rendering;

public static class ImGuiInterop
{
    private static GL? _gl;
    private static IInputContext? _inputContext;
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
            _gl = gl;
            _inputContext = window.CreateInput();

            var io = ImGui.GetIO();
            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
            io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors;

            HookInputEvents();
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

    private static void HookInputEvents()
    {
        if (_inputContext == null) return;

        foreach (var keyboard in _inputContext.Keyboards)
        {
            keyboard.KeyDown += OnKeyDown;
            keyboard.KeyUp += OnKeyUp;
            keyboard.KeyChar += OnKeyChar;
        }

        foreach (var mouse in _inputContext.Mice)
        {
            mouse.MouseDown += OnMouseDown;
            mouse.MouseUp += OnMouseUp;
            mouse.Scroll += OnMouseScroll;
        }
    }

    private static void OnKeyDown(IKeyboard keyboard, Key key, int scanCode)
    {
        UpdateKeyModifiers(keyboard);
        AddKeyEvent(key, scanCode, true);
    }

    private static void OnKeyUp(IKeyboard keyboard, Key key, int scanCode)
    {
        UpdateKeyModifiers(keyboard);
        AddKeyEvent(key, scanCode, false);
    }

    private static void OnKeyChar(IKeyboard keyboard, char character)
    {
        if (!char.IsControl(character))
        {
            ImGui.GetIO().AddInputCharacter(character);
        }
    }

    private static void OnMouseDown(IMouse mouse, MouseButton button)
    {
        int index = MouseButtonIndex(button);
        if (index >= 0)
        {
            ImGui.GetIO().AddMouseButtonEvent(index, true);
        }
    }

    private static void OnMouseUp(IMouse mouse, MouseButton button)
    {
        int index = MouseButtonIndex(button);
        if (index >= 0)
        {
            ImGui.GetIO().AddMouseButtonEvent(index, false);
        }
    }

    private static void OnMouseScroll(IMouse mouse, ScrollWheel wheel)
    {
        ImGui.GetIO().AddMouseWheelEvent(wheel.X, wheel.Y);
    }

    private static void AddKeyEvent(Key key, int scanCode, bool down)
    {
        var imguiKey = ToImGuiKey(key);
        if (imguiKey == ImGuiKey.None) return;

        var io = ImGui.GetIO();
        io.AddKeyEvent(imguiKey, down);
        io.SetKeyEventNativeData(imguiKey, (int)key, scanCode);
    }

    private static void UpdateKeyModifiers(IKeyboard keyboard)
    {
        var io = ImGui.GetIO();
        io.AddKeyEvent(ImGuiKey.ModCtrl, keyboard.IsKeyPressed(Key.ControlLeft) || keyboard.IsKeyPressed(Key.ControlRight));
        io.AddKeyEvent(ImGuiKey.ModShift, keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight));
        io.AddKeyEvent(ImGuiKey.ModAlt, keyboard.IsKeyPressed(Key.AltLeft) || keyboard.IsKeyPressed(Key.AltRight));
        io.AddKeyEvent(ImGuiKey.ModSuper, keyboard.IsKeyPressed(Key.SuperLeft) || keyboard.IsKeyPressed(Key.SuperRight));
    }

    private static int MouseButtonIndex(MouseButton button) => button switch
    {
        MouseButton.Left => 0,
        MouseButton.Right => 1,
        MouseButton.Middle => 2,
        MouseButton.Button4 => 3,
        MouseButton.Button5 => 4,
        _ => -1,
    };

    private static ImGuiKey ToImGuiKey(Key key) => key switch
    {
        Key.Tab => ImGuiKey.Tab,
        Key.Left => ImGuiKey.LeftArrow,
        Key.Right => ImGuiKey.RightArrow,
        Key.Up => ImGuiKey.UpArrow,
        Key.Down => ImGuiKey.DownArrow,
        Key.PageUp => ImGuiKey.PageUp,
        Key.PageDown => ImGuiKey.PageDown,
        Key.Home => ImGuiKey.Home,
        Key.End => ImGuiKey.End,
        Key.Insert => ImGuiKey.Insert,
        Key.Delete => ImGuiKey.Delete,
        Key.Backspace => ImGuiKey.Backspace,
        Key.Space => ImGuiKey.Space,
        Key.Enter or Key.KeypadEnter => ImGuiKey.Enter,
        Key.Escape => ImGuiKey.Escape,
        Key.ControlLeft => ImGuiKey.LeftCtrl,
        Key.ShiftLeft => ImGuiKey.LeftShift,
        Key.AltLeft => ImGuiKey.LeftAlt,
        Key.SuperLeft => ImGuiKey.LeftSuper,
        Key.ControlRight => ImGuiKey.RightCtrl,
        Key.ShiftRight => ImGuiKey.RightShift,
        Key.AltRight => ImGuiKey.RightAlt,
        Key.SuperRight => ImGuiKey.RightSuper,
        Key.Menu => ImGuiKey.Menu,
        Key.Number0 or Key.D0 => ImGuiKey._0,
        Key.Number1 => ImGuiKey._1,
        Key.Number2 => ImGuiKey._2,
        Key.Number3 => ImGuiKey._3,
        Key.Number4 => ImGuiKey._4,
        Key.Number5 => ImGuiKey._5,
        Key.Number6 => ImGuiKey._6,
        Key.Number7 => ImGuiKey._7,
        Key.Number8 => ImGuiKey._8,
        Key.Number9 => ImGuiKey._9,
        Key.A => ImGuiKey.A,
        Key.B => ImGuiKey.B,
        Key.C => ImGuiKey.C,
        Key.D => ImGuiKey.D,
        Key.E => ImGuiKey.E,
        Key.F => ImGuiKey.F,
        Key.G => ImGuiKey.G,
        Key.H => ImGuiKey.H,
        Key.I => ImGuiKey.I,
        Key.J => ImGuiKey.J,
        Key.K => ImGuiKey.K,
        Key.L => ImGuiKey.L,
        Key.M => ImGuiKey.M,
        Key.N => ImGuiKey.N,
        Key.O => ImGuiKey.O,
        Key.P => ImGuiKey.P,
        Key.Q => ImGuiKey.Q,
        Key.R => ImGuiKey.R,
        Key.S => ImGuiKey.S,
        Key.T => ImGuiKey.T,
        Key.U => ImGuiKey.U,
        Key.V => ImGuiKey.V,
        Key.W => ImGuiKey.W,
        Key.X => ImGuiKey.X,
        Key.Y => ImGuiKey.Y,
        Key.Z => ImGuiKey.Z,
        Key.F1 => ImGuiKey.F1,
        Key.F2 => ImGuiKey.F2,
        Key.F3 => ImGuiKey.F3,
        Key.F4 => ImGuiKey.F4,
        Key.F5 => ImGuiKey.F5,
        Key.F6 => ImGuiKey.F6,
        Key.F7 => ImGuiKey.F7,
        Key.F8 => ImGuiKey.F8,
        Key.F9 => ImGuiKey.F9,
        Key.F10 => ImGuiKey.F10,
        Key.F11 => ImGuiKey.F11,
        Key.F12 => ImGuiKey.F12,
        Key.Apostrophe => ImGuiKey.Apostrophe,
        Key.Comma => ImGuiKey.Comma,
        Key.Minus => ImGuiKey.Minus,
        Key.Period => ImGuiKey.Period,
        Key.Slash => ImGuiKey.Slash,
        Key.Semicolon => ImGuiKey.Semicolon,
        Key.Equal => ImGuiKey.Equal,
        Key.LeftBracket => ImGuiKey.LeftBracket,
        Key.BackSlash => ImGuiKey.Backslash,
        Key.RightBracket => ImGuiKey.RightBracket,
        Key.GraveAccent => ImGuiKey.GraveAccent,
        Key.CapsLock => ImGuiKey.CapsLock,
        Key.ScrollLock => ImGuiKey.ScrollLock,
        Key.NumLock => ImGuiKey.NumLock,
        Key.PrintScreen => ImGuiKey.PrintScreen,
        Key.Pause => ImGuiKey.Pause,
        Key.Keypad0 => ImGuiKey.Keypad0,
        Key.Keypad1 => ImGuiKey.Keypad1,
        Key.Keypad2 => ImGuiKey.Keypad2,
        Key.Keypad3 => ImGuiKey.Keypad3,
        Key.Keypad4 => ImGuiKey.Keypad4,
        Key.Keypad5 => ImGuiKey.Keypad5,
        Key.Keypad6 => ImGuiKey.Keypad6,
        Key.Keypad7 => ImGuiKey.Keypad7,
        Key.Keypad8 => ImGuiKey.Keypad8,
        Key.Keypad9 => ImGuiKey.Keypad9,
        Key.KeypadDecimal => ImGuiKey.KeypadDecimal,
        Key.KeypadDivide => ImGuiKey.KeypadDivide,
        Key.KeypadMultiply => ImGuiKey.KeypadMultiply,
        Key.KeypadSubtract => ImGuiKey.KeypadSubtract,
        Key.KeypadAdd => ImGuiKey.KeypadAdd,
        Key.KeypadEqual => ImGuiKey.KeypadEqual,
        _ => ImGuiKey.None,
    };

    private static unsafe void CreateDeviceObjects()
    {
        if (_gl == null) return;

        var io = ImGui.GetIO();

        IntPtr pixels;
        int width, height;
        io.Fonts.GetTexDataAsRGBA32(out pixels, out width, out height);

        _textureId = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _textureId);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels.ToPointer());
        
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
        _gl.VertexAttribPointer(0, 2, GLEnum.Float, false, (uint)sizeof(ImDrawVert), (void*)0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 2, GLEnum.Float, false, (uint)sizeof(ImDrawVert), (void*)(2 * sizeof(float)));
        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribPointer(2, 4, GLEnum.UnsignedByte, true, (uint)sizeof(ImDrawVert), (void*)(4 * sizeof(float)));

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

        var mouse = _inputContext?.Mice.FirstOrDefault();
        if (mouse != null)
        {
            io.AddMousePosEvent(mouse.Position.X, mouse.Position.Y);
        }

        ImGui.NewFrame();
    }

    public static unsafe void Render(GL gl)
    {
        ImGui.Render();

        var drawData = ImGui.GetDrawData();
        if (drawData.NativePtr == null || drawData.CmdListsCount == 0) return;

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
        gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);
        gl.Disable(EnableCap.CullFace);
        gl.Disable(EnableCap.DepthTest);
        gl.Enable(EnableCap.ScissorTest);

        gl.UseProgram((uint)_shaderProgram);
        int loc = gl.GetUniformLocation((uint)_shaderProgram, "ProjMtx");
        fixed (float* projPtr = projMtx)
        {
            gl.UniformMatrix4(loc, 1, false, projPtr);
        }

        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _textureId);

        gl.BindVertexArray((uint)_vao);

        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmdList = drawData.CmdLists[n];

            var vtxBuffer = cmdList.VtxBuffer;
            var idxBuffer = cmdList.IdxBuffer;

            gl.BindBuffer(BufferTargetARB.ArrayBuffer, (uint)_vbo);
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vtxBuffer.Size * sizeof(ImDrawVert)), vtxBuffer.Data.ToPointer(), BufferUsageARB.DynamicDraw);

            gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, (uint)_ebo);
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(idxBuffer.Size * sizeof(ushort)), idxBuffer.Data.ToPointer(), BufferUsageARB.DynamicDraw);

            for (int cmdIdx = 0; cmdIdx < cmdList.CmdBuffer.Size; cmdIdx++)
            {
                var cmd = cmdList.CmdBuffer[cmdIdx];

                if (cmd.UserCallback != IntPtr.Zero) continue;

                float left = drawData.DisplayPos.X + cmd.ClipRect.X;
                float top = drawData.DisplayPos.Y + cmd.ClipRect.Y;
                float right = drawData.DisplayPos.X + cmd.ClipRect.Z;
                float bottom = drawData.DisplayPos.Y + cmd.ClipRect.W;

                gl.Scissor((int)Math.Max(0, left), (int)Math.Max(0, io.DisplaySize.Y - bottom), (uint)Math.Max(0, right - left), (uint)Math.Max(0, bottom - top));

                if (cmd.TextureId != IntPtr.Zero)
                {
                    gl.BindTexture(TextureTarget.Texture2D, (uint)cmd.TextureId.ToPointer());
                }

                gl.DrawElementsBaseVertex(PrimitiveType.Triangles, cmd.ElemCount, DrawElementsType.UnsignedShort, (void*)(cmd.IdxOffset * sizeof(ushort)), (int)cmd.VtxOffset);
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
}
