using System.Text;
using static BitMagic.X16Emulator.Emulator;
using Silk.NET.Input;

namespace BitMagic.X16Emulator;

public class SmcBuffer
{
    [Flags]
    public enum MouseButtons
    {
        None,
        Left = 1,
        Right = 2,
        Middle = 4,
    }

    private readonly Emulator _emulator;

    public SmcBuffer(Emulator emulator)
    {
        _emulator = emulator;
    }

    public void KeyDown(Key key) => AddKey(true, KeyToIbmScanCode(key));

    public void KeyUp(Key key) => AddKey(false, KeyToIbmScanCode(key));

    public void AddKey(bool keyDown, byte scancode)
    {
        PushKeyboard((byte)(scancode | (keyDown ? 0x00 : 0x80)));
    }

    public void PushKeyboard(byte value)
    {
        //Console.WriteLine($"Key press : {value:X2} {value & 0x7f}");
        var next = (_emulator.Keyboard_WritePosition + 1) & (Emulator.SmcKeyboardBufferSize - 1);
        if (next != _emulator.Keyboard_ReadPosition)
        {
            _emulator.KeyboardBuffer[(int)_emulator.Keyboard_WritePosition] = value;
            _emulator.Keyboard_WritePosition = next;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Warning: Keyboard buffer full! Cannot add ${value:X2}");
            Console.ResetColor();
        }
    }

    public void PushMouse(int xDelta, int yDelta, MouseButtons buttons)
    {
        while (true)
        {
            // discard movements if they would overfill the biffer
            var length = (Emulator.SmcMouseBufferSize + _emulator.Mouse_WritePosition - _emulator.Mouse_ReadPosition) & (Emulator.SmcMouseBufferSize - 1);
            if (length >= 6)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Warning: Mouse buffer full!");
                Console.ResetColor();
                return;
            }

            yDelta *= -1;
            var toSendX = xDelta > 255 ? 255 : xDelta < -256 ? -256 : xDelta;
            var toSendY = yDelta > 255 ? 255 : yDelta < -256 ? -256 : yDelta;

            xDelta -= toSendX;
            yDelta -= toSendY;

            byte btns = (byte)((byte)buttons + (byte)0b1000 + (byte)((toSendX & 0x100) != 0 ? 010000 : 0) + (byte)((toSendY & 0x100) != 0 ? 0100000 : 0));

            //Console.WriteLine($"{Convert.ToString(btns, 2)} {(byte)toSendX:X2} {(byte)toSendY:X2}");
            PushMouseByte(btns);
            PushMouseByte((byte)toSendX);
            PushMouseByte((byte)toSendY);
            //PushMouseByte(0);

            if (xDelta == 0 && yDelta == 0)
                return;
        }
    }

    public void PushMouseByte(byte value)
    {
        var next = (_emulator.Mouse_WritePosition + 1) & (Emulator.SmcMouseBufferSize - 1);
        if (next != _emulator.Mouse_ReadPosition)
        {
            _emulator.MouseBuffer[(int)_emulator.Mouse_WritePosition] = value;
            _emulator.Mouse_WritePosition = next;
        }
    }

    private const uint EXTENDED_FLAG = 0x100;
    public uint KeyToPs2ScanCode(Key key) => key switch
    {
        // SDL_SCANCODE_CLEAR
        Key.GraveAccent => 0x0e,
        Key.Backspace => 0x66,
        Key.Tab => 0x0d,
        Key.Enter => 0x5a,
        Key.Pause => 0x00,
        Key.Escape => 0xff, // Esc is 0x76, but we send break
        Key.Space => 0x29,
        Key.Apostrophe => 0x52,
        Key.Comma => 0x41,
        Key.Minus => 0x4e,
        Key.Period => 0x49,
        Key.Slash => 0x4a,
        Key.Number0 => 0x45,
        Key.Number1 => 0x16,
        Key.Number2 => 0x1e,
        Key.Number3 => 0x26,
        Key.Number4 => 0x25,
        Key.Number5 => 0x2e,
        Key.Number6 => 0x36,
        Key.Number7 => 0x3d,
        Key.Number8 => 0x3e,
        Key.Number9 => 0x46,
        Key.Semicolon => 0x4c,
        Key.Equal => 0x55,
        Key.LeftBracket => 0x54,
        Key.BackSlash => 0x5d,
        Key.RightBracket => 0x5b,
        Key.A => 0x1c,
        Key.B => 0x32,
        Key.C => 0x21,
        Key.D => 0x23,
        Key.E => 0x24,
        Key.F => 0x2b,
        Key.G => 0x34,
        Key.H => 0x33,
        Key.I => 0x43,
        Key.J => 0x3B,
        Key.K => 0x42,
        Key.L => 0x4B,
        Key.M => 0x3A,
        Key.N => 0x31,
        Key.O => 0x44,
        Key.P => 0x4D,
        Key.Q => 0x15,
        Key.R => 0x2D,
        Key.S => 0x1B,
        Key.T => 0x2C,
        Key.U => 0x3C,
        Key.V => 0x2A,
        Key.W => 0x1D,
        Key.X => 0x22,
        Key.Y => 0x35,
        Key.Z => 0x1A,
        Key.Delete => 0x71 | EXTENDED_FLAG,
        Key.Up => 0x75 | EXTENDED_FLAG,
        Key.Down => 0x72 | EXTENDED_FLAG,
        Key.Right => 0x74 | EXTENDED_FLAG,
        Key.Left => 0x6b | EXTENDED_FLAG,
        Key.Insert => 0x70 | EXTENDED_FLAG,
        Key.Home => 0x6c | EXTENDED_FLAG,
        Key.End => 0x69 | EXTENDED_FLAG,
        Key.PageUp => 0x7d | EXTENDED_FLAG,
        Key.PageDown => 0x7a | EXTENDED_FLAG,
        Key.F1 => 0x05,
        Key.F2 => 0x06,
        Key.F3 => 0x04,
        Key.F4 => 0x0c,
        Key.F5 => 0x03,
        Key.F6 => 0x0b,
        Key.F7 => 0x83,
        Key.F8 => 0x0a,
        Key.F9 => 0x01,
        Key.F10 => 0x09,
        Key.F11 => 0x78,
        Key.F12 => 0x07,
        Key.ShiftRight => 0x59,
        Key.ShiftLeft => 0x12,
        Key.CapsLock => 0x58,
        Key.ControlLeft => 0x14,
        Key.ControlRight => 0x14 | EXTENDED_FLAG,
        Key.AltLeft => 0x11,
        Key.AltRight => 0x11 | EXTENDED_FLAG,
        //SDL_SCANCODE_LGUI // Windows/Command
        //SDL_SCANCODE_RGUI => 0x5b | EXTENDED_FLAG,
        Key.Menu => 0x2f | EXTENDED_FLAG,
        //SDL_SCANCODE_NONUSBACKSLASH => 0x61,
        Key.KeypadEnter => 0x5a | EXTENDED_FLAG,
        Key.Keypad0 => 0x70,
        Key.Keypad1 => 0x69,
        Key.Keypad2 => 0x72,
        Key.Keypad3 => 0x7a,
        Key.Keypad4 => 0x6b,
        Key.Keypad5 => 0x73,
        Key.Keypad6 => 0x74,
        Key.Keypad7 => 0x6c,
        Key.Keypad8 => 0x75,
        Key.Keypad9 => 0x7d,
        Key.KeypadDecimal => 0x71,
        Key.KeypadAdd => 0x79,
        Key.KeypadSubtract => 0x7b,
        Key.KeypadMultiply => 0x7c,
        Key.KeypadDivide => 0x4a | EXTENDED_FLAG,
        _ => 0
    };

    public byte KeyToIbmScanCode(Key key) => key switch
    {
        // SDL_SCANCODE_CLEAR
        Key.GraveAccent => 1,
        Key.Backspace => 15,
        Key.Tab => 16,
        Key.Enter => 43,
        Key.Pause => 126,
        Key.Escape => 110, //126, // 126 is break, 110 is esc
        Key.Space => 61,
        Key.Apostrophe => 41,
        Key.Comma => 53,
        Key.Minus => 12,
        Key.Period => 54,
        Key.Slash => 55,
        Key.Number0 => 11,
        Key.Number1 => 2,
        Key.Number2 => 3,
        Key.Number3 => 4,
        Key.Number4 => 5,
        Key.Number5 => 6,
        Key.Number6 => 7,
        Key.Number7 => 8,
        Key.Number8 => 9,
        Key.Number9 => 10,
        Key.Semicolon => 40,
        Key.Equal => 13,
        Key.LeftBracket => 27,
        Key.BackSlash => 29,
        Key.RightBracket => 28,
        Key.A => 31,
        Key.B => 50,
        Key.C => 48,
        Key.D => 33,
        Key.E => 19,
        Key.F => 34,
        Key.G => 35,
        Key.H => 36,
        Key.I => 24,
        Key.J => 37,
        Key.K => 38,
        Key.L => 39,
        Key.M => 52,
        Key.N => 51,
        Key.O => 25,
        Key.P => 26,
        Key.Q => 17,
        Key.R => 20,
        Key.S => 32,
        Key.T => 21,
        Key.U => 23,
        Key.V => 49,
        Key.W => 18,
        Key.X => 47,
        Key.Y => 22,
        Key.Z => 46,
        Key.Delete => 76,
        Key.Up => 83,
        Key.Down => 84,
        Key.Right => 89,
        Key.Left => 79,
        Key.Insert => 75,
        Key.Home => 80,
        Key.End => 81,
        Key.PageUp => 85,
        Key.PageDown => 86,
        Key.F1 => 112,
        Key.F2 => 113,
        Key.F3 => 114,
        Key.F4 => 115,
        Key.F5 => 116,
        Key.F6 => 117,
        Key.F7 => 118,
        Key.F8 => 119,
        Key.F9 => 120,
        Key.F10 => 121,
        Key.F11 => 122,
        Key.F12 => 123,
        Key.ShiftRight => 57,
        Key.ShiftLeft => 44,
        Key.ScrollLock => 125,
        Key.CapsLock => 30,
        Key.ControlLeft => 58,
        Key.ControlRight => 64,
        Key.AltLeft => 60,
        Key.AltRight => 62,
        //SDL_SCANCODE_LGUI // Windows/Command
        //SDL_SCANCODE_RGUI => 0x5b | EXTENDED_FLAG,
        Key.Menu => 0,
        //SDL_SCANCODE_NONUSBACKSLASH => 0x61,
        Key.KeypadEnter => 108,
        Key.Keypad0 => 99,
        Key.Keypad1 => 93,
        Key.Keypad2 => 98,
        Key.Keypad3 => 103,
        Key.Keypad4 => 92,
        Key.Keypad5 => 97,
        Key.Keypad6 => 102,
        Key.Keypad7 => 91,
        Key.Keypad8 => 96,
        Key.Keypad9 => 101,
        Key.KeypadDecimal => 104,
        Key.KeypadAdd => 106,
        Key.KeypadSubtract => 105,
        Key.KeypadMultiply => 100,
        Key.KeypadDivide => 95,
        _ => 0
    };

    private bool _running = true;

    public unsafe void RunI2cCapture(ref CpuState state)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"SDA\tSCL\tmode");
        uint lastPosition = state.I2cPosition;
        var buffer = new Span<byte>((void*)state.I2cBufferPtr, 1024);
        while (_running)
        {
            var thisPosition = state.I2cPosition;
            if (thisPosition != lastPosition)
            {
                while (thisPosition != lastPosition)
                {
                    var val = buffer[(int)lastPosition] & 0x03;
                    sb.Append($"{(val & 0x01) + 1}\t{(val & 0x02) >> 1}\t");
                    //sb.AppendLine($"SDA: {val & 0x01}\tSCL: {(val & 0x02) >> 1}");
                    lastPosition++;
                    if (lastPosition >= 1024) lastPosition = 0;

                    val = buffer[(int)lastPosition];
                    sb.AppendLine($"{val}");
                    //sb.AppendLine($"SDA: {val & 0x01}\tSCL: {(val & 0x02) >> 1}");
                    lastPosition++;
                    if (lastPosition >= 1024) lastPosition = 0;

                }
            }
            else
                Thread.Sleep(1);
        }

        File.WriteAllText(@"c:\documents\Source\i2c.txt", sb.ToString());
    }

    public void Stop()
    {
        _running = false;
    }
}
