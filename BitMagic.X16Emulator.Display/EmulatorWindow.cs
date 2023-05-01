using BitMagic.Common;
using BitMagic.X16Emulator;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using static System.Collections.Specialized.BitVector32;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;
using Silk.NET.Core;
using Silk.NET.Input;
using SixLabors.ImageSharp.PixelFormats;
using Silk.NET.Core.Attributes;
using System.Numerics;
//using Silk.NET.SDL;

namespace BitMagic.X16Emulator.Display;

public class EmulatorWindow
{
    private static GL? _gl;
    private static IWindow? _window;
    private static Shader? _shader;
    private static X16EImage[]? _images;

    private static GlObject[]? _layers;
    private static UInt32 _lastCount;
    private static long _lastTicks;
    private static double _speed = 0;
    private static double _fps = 0;
    private static Stopwatch _stopwatch = new Stopwatch();
    private static Emulator? _emulator;
    private static bool _closing = false;
    private static bool _hasMouse = false;
    private static Vector2 _lastMousePosition;
    private static IGamepad[]? _joysticks;
    
    private static int _mouseX = 0;
    private static int _mouseY = 0;
    private static Timer? _mouseTimer = null;

    public static void Run(Emulator emulator)
    {
        _closing = false;
        _emulator = emulator;
        _window = Window.Create(WindowOptions.Default);

        _images = new X16EImage[6];
        _images[0] = new X16EImage(_emulator, 0);
        _images[1] = new X16EImage(_emulator, 1);
        _images[2] = new X16EImage(_emulator, 2);
        _images[3] = new X16EImage(_emulator, 3);
        _images[4] = new X16EImage(_emulator, 4);
        _images[5] = new X16EImage(_emulator, 5);

        _window.Size = new Silk.NET.Maths.Vector2D<int> { X = 800, Y = 525 };
        _window.Title = "BitMagic! X16E";
        _window.WindowBorder = WindowBorder.Fixed;

        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.Closing += OnClose;
        
        _stopwatch.Start();

        _window.Run();
    }

    private static void EmulatorWindow_KeyUp(IKeyboard arg1, Key arg2, int arg3) => _emulator!.SmcBuffer.KeyUp(arg2);
    private static void EmulatorWindow_KeyDown(IKeyboard arg1, Key arg2, int arg3) => _emulator!.SmcBuffer.KeyDown(arg2);

    private static unsafe void OnLoad()
    {
        if (_window == null) throw new Exception("_window not set");
        if (_images == null) throw new Exception("_images not set");

        var input = _window.CreateInput();
        input.Keyboards[0].KeyUp += EmulatorWindow_KeyUp;
        input.Keyboards[0].KeyDown += EmulatorWindow_KeyDown;

        input.Mice[0].Cursor.CursorMode = CursorMode.Normal;
        input.Mice[0].DoubleClick += EmulatorWindow_Click;
        input.Mice[0].MouseUp += EmulatorWindow_MouseUp;
        input.Mice[0].MouseDown += EmulatorWindow_MouseDown;
        input.Mice[0].MouseMove += EmulatorWindow_MouseMove;

        _joysticks = input.Gamepads.Take(4).ToArray();
        _emulator!.JoystickData = 0;
        _emulator.JoystickNewMask = 0;

        for (var i = 0; i < 4; i++)
        {
            var bitshift = (3 - i) * 16;
            var mask = (ulong)0xffff << bitshift;

            if (i >= _joysticks.Length || _joysticks[i] == null)
            {
                _emulator.JoystickData |= (ulong)0xffff << bitshift;
                _emulator.JoystickNewMask |= (ulong)0x8000 << bitshift; // disconnected joypads always have 1, connected 0
                continue;
            }

            var initData = (ulong)0x0000 << bitshift;
            _emulator.JoystickNewMask |= (ulong)0x0000 << bitshift; // disconnected joypads always have 1, connected 0
            _emulator.JoystickData |= initData;

            var button_b = _joysticks[i].B().Index;
            var button_y = _joysticks[i].Y().Index;
            var button_select = _joysticks[i].Back().Index;
            var button_start = _joysticks[i].Start().Index;
            var button_up = _joysticks[i].DPadUp().Index;
            var button_down = _joysticks[i].DPadDown().Index;
            var button_left = _joysticks[i].DPadLeft().Index;
            var button_right = _joysticks[i].DPadRight().Index;
            var button_a = _joysticks[i].A().Index;
            var button_x = _joysticks[i].X().Index;
            var button_l = _joysticks[i].LeftBumper().Index;
            var button_r = _joysticks[i].RightBumper().Index;

            var joystickDelegate = (IGamepad g, Button _) =>
            {
                ulong buttons = 0xf000; // top 4 bytes are always 1 (which is a 0 to the x16 apparently)
                buttons |= !g.Buttons[button_b].Pressed ? 0b1 : (ulong)0;
                buttons |= !g.Buttons[button_y].Pressed ? 0b10 : (ulong)0;
                buttons |= !g.Buttons[button_select].Pressed ? 0b100 : (ulong)0;
                buttons |= !g.Buttons[button_start].Pressed ? 0b1000 : (ulong)0;
                buttons |= !g.Buttons[button_up].Pressed ? 0b10000 : (ulong)0;
                buttons |= !g.Buttons[button_down].Pressed ? 0b100000 : (ulong)0;
                buttons |= !g.Buttons[button_left].Pressed ? 0b1000000 : (ulong)0;
                buttons |= !g.Buttons[button_right].Pressed ? 0b10000000 : (ulong)0;
                buttons |= !g.Buttons[button_a].Pressed ? 0b000100000000 : (ulong)0;
                buttons |= !g.Buttons[button_x].Pressed ? 0b001000000000 : (ulong)0;
                buttons |= !g.Buttons[button_l].Pressed ? 0b010000000000 : (ulong)0;
                buttons |= !g.Buttons[button_r].Pressed ? 0b100000000000 : (ulong)0;
                buttons <<= bitshift;

                var data = _emulator.JoystickData & ~mask;
                data |= buttons;
                _emulator.JoystickData = data;
            };

            joystickDelegate(_joysticks[i], new Button());  // get init state
            _joysticks[i].ButtonDown += joystickDelegate;
            _joysticks[i].ButtonUp += joystickDelegate;
        }

        _window.FocusChanged += _window_FocusChanged;

        _window.SetDefaultIcon();
        _gl = GL.GetApi(_window);

        _layers = new GlObject[_images.Length];

        for (var i = 0; i < _images.Length; i++)
        {
            _layers[i] = new GlObject();
            _layers[i].OnLoad(_gl, _images[i], i / 10f);
        }

        _shader = new Shader(_gl, @"shader.vert", @"shader.frag");
        _emulator!.Control = Control.Run;

        #if OS_WINDOWS

        var assembly = Assembly.GetEntryAssembly();
        if (assembly != null)
        {
            string resourceName = assembly.GetManifestResourceNames().Single(str => str.EndsWith("butterfly.jpg"));
            using (Stream stream = assembly.GetManifestResourceStream(resourceName) ?? throw new Exception("butterfly.jpg not found"))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                var icon = Image.Load<Rgba32>(reader.ReadBytes((int)stream.Length)) ?? throw new Exception("icon image is null");
                var silkIcon = new byte[icon.Width * icon.Height * 4];

                var index = 0;
                for (var y = 0; y < icon.Height; y++)
                {
                    for (var x = 0; x < icon.Width; x++)
                    {
                        var pixel = icon[x, y];
                        silkIcon[index++] = pixel.R;
                        silkIcon[index++] = pixel.G;
                        silkIcon[index++] = pixel.B;
                        silkIcon[index++] = pixel.A;
                    }
                }

                var rawIcon = new RawImage(icon.Width, icon.Height, new Memory<byte>(silkIcon));

                _window.SetWindowIcon(ref rawIcon);
            
            }
        }

        #endif
    }


    // If we lose focus, then stop capturing the mouse
    private static void _window_FocusChanged(bool obj)
    {
        if (obj || !_hasMouse)
            return;

        var input = _window.CreateInput();
        input.Mice[0].Cursor.CursorMode = CursorMode.Normal;
        _hasMouse = false;
        _mouseTimer?.Dispose();
    }

    // If we click then we capture the mouse and pass movement to the emulator.
    private static void EmulatorWindow_Click(IMouse arg1, Silk.NET.Input.MouseButton arg2, System.Numerics.Vector2 arg3)
    {
        if (_hasMouse)
            return;

        var input = _window.CreateInput();
        input.Mice[0].Cursor.CursorMode = CursorMode.Raw;
        _hasMouse = true;
        _mouseTimer = new Timer(CheckMouseMove, null, 20, 20);
        _mouseX = 0;
        _mouseX = 1;
    }

    private static void EmulatorWindow_MouseMove(IMouse arg1, System.Numerics.Vector2 position)
    {
        if (!_hasMouse)
            return;

        if (_lastMousePosition == default)
            _lastMousePosition = position;
        else
        {
            var xDelta = (int)(position.X - _lastMousePosition.X);
            var yDelta = (int)(position.Y - _lastMousePosition.Y);

            _lastMousePosition = position;
            _mouseX += xDelta;
            _mouseY += yDelta;
            //_emulator.SmcBuffer.PushMouse(xDelta, yDelta, GetButtons(arg1));
        }
    }

    private static void EmulatorWindow_MouseDown(IMouse arg1, Silk.NET.Input.MouseButton arg2)
    {
        if (!_hasMouse)
            return;

        _emulator.SmcBuffer.PushMouse(0, 0, GetButtons(arg1));
    }

    private static void EmulatorWindow_MouseUp(IMouse arg1, Silk.NET.Input.MouseButton arg2)
    {
        if (!_hasMouse)
            return;

        _emulator.SmcBuffer.PushMouse(0, 0, GetButtons(arg1));
    }

    private static SmcBuffer.MouseButtons GetButtons(IMouse mouse) =>
        (SmcBuffer.MouseButtons)((mouse.IsButtonPressed(Silk.NET.Input.MouseButton.Left) ? (int)SmcBuffer.MouseButtons.Left : 0) +
            (mouse.IsButtonPressed(Silk.NET.Input.MouseButton.Right) ? (int)SmcBuffer.MouseButtons.Right : 0) +
            (mouse.IsButtonPressed(Silk.NET.Input.MouseButton.Middle) ? (int)SmcBuffer.MouseButtons.Middle : 0));


    private static void CheckMouseMove(Object? stateInfo)
    {
        if (_mouseX == 0 && _mouseY == 0)
            return;

        _emulator.SmcBuffer.PushMouse(_mouseX, _mouseY, SmcBuffer.MouseButtons.None);
        _mouseX = 0;
        _mouseY = 0;
    }

    private static unsafe void OnRender(double deltaTime)
    {
        if (_closing) return;
        if (_gl == null) throw new ArgumentNullException(nameof(_gl));
        if (_shader == null) throw new ArgumentNullException(nameof(_shader));
        if (_layers == null) throw new ArgumentNullException(nameof(_layers));

        //_gl.Enable(EnableCap.DepthTest);
        //_gl.Enable(GLEnum.Blend);
        // _gl.BlendFunc(BlendingFactor.SrcColor, BlendingFactor.SrcColor);
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        //_layers[0].OnRender(_gl, _shader, _requireUpdate);

        foreach (var i in _layers)
        {
            if (_closing) return;
            i.OnRender(_gl, _shader, _emulator!.RenderReady);
        }

        _emulator!.RenderReady = false;
        var thisTicks = _stopwatch.ElapsedMilliseconds;
        if (thisTicks - _lastTicks > 1000)
        {
            var thisCount = _emulator.Vera.Frame_Count;

            if (thisCount == _lastCount) // no frames this second?
            {
                _speed = 0;
                _fps = 0;
            }
            else
            {
                var tickDelta = thisTicks - _lastTicks;
                _fps = (thisCount - _lastCount) / (tickDelta / 1000.0);
                _speed = _fps / 59.523809;
            }
            _lastCount = thisCount;
            _lastTicks = thisTicks;

            _window!.Title = $"BitMagic! X16E [{_speed:0.00%} \\ {_fps:0.0} fps \\ {_speed * 8.0:0}Mhz] {(_hasMouse ? "* MOUSE CAPTURED *" : "")}";
        }

        _emulator.Control = Control.Run;
    }

    private static void OnClose()
    {
        _closing = true;
        _gl?.Dispose();
        _shader?.Dispose();
        if (_layers != null)
        {
            foreach (var i in _layers)
            {
                i.Dispose();
            }
        }
        _emulator!.Control = Control.Stop;

        _gl = null;
        _window = null;
        _shader = null;
        _images = null;
        _layers = null;
        _emulator = null;
        _mouseTimer?.Dispose();
    }

    public static void Stop()
    {
        _closing = true;
        _window?.Close();
    }
}
