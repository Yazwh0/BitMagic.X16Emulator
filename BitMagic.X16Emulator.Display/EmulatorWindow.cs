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

        _window.FocusChanged += _window_FocusChanged;

        _window.SetDefaultIcon();
        _gl = GL.GetApi(_window);

        _layers = new GlObject[_images.Length];

        for (var i = 0; i < _images.Length; i++)
        {
            _layers[i] = new GlObject();
            _layers[i].OnLoad(_gl, _images[i], i / 10);
        }

        _shader = new Shader(_gl, @"shader.vert", @"shader.frag");
        _emulator!.Control = Control.Run;

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
    }

    // If we lose focus, then stop capturing the mouse
    private static void _window_FocusChanged(bool obj)
    {
        if (obj || !_hasMouse)
            return;

        var input = _window.CreateInput();
        input.Mice[0].Cursor.CursorMode = CursorMode.Normal;
        _hasMouse = false;
    }

    // If we click then we capture the mouse and pass movement to the emulator.
    private static void EmulatorWindow_Click(IMouse arg1, Silk.NET.Input.MouseButton arg2, System.Numerics.Vector2 arg3)
    {
        if (_hasMouse)
            return;

        var input = _window.CreateInput();
        input.Mice[0].Cursor.CursorMode = CursorMode.Raw;
        _hasMouse = true;
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
            _emulator.SmcBuffer.PushMouse(xDelta, yDelta, GetButtons(arg1));
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
    }

    public static void Stop()
    {
        _closing = true;
        _window?.Close();
    }
}
