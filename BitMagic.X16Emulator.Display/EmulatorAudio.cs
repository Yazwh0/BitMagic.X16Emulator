//#define LOG_OUTPUT

using Silk.NET.Core;
using Silk.NET.Core.Contexts;
using Silk.NET.SDL;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Thread = System.Threading.Thread;

namespace BitMagic.X16Emulator.Display;

public unsafe class EmulatorAudio : IDisposable
{
    private readonly uint _audio_device;
    private readonly Sdl _sdl;
    private readonly Emulator _emulator;
    private uint _bufferRead;
    private readonly uint _bufferMask;
    private readonly uint _bufferSize;
    private readonly ulong _ptr;
    public uint Delay { get; private set; }
    #if LOG_OUTPUT
    private readonly StreamWriter _writer;
    #endif

    public EmulatorAudio(Emulator emulator)
    {
        _bufferMask = Emulator.AudioOutputSize / 4 - 1 - 1;
        _bufferSize = Emulator.AudioOutputSize / 4;

        _emulator = emulator;
        _sdl = new Sdl(new DefaultNativeContext("SDL2"));

        _sdl.Init(Sdl.InitAudio);

        var desired = new AudioSpec();

        desired.Freq = 25000000 / 512;
        desired.Format = Sdl.AudioS16Sys;
        desired.Channels = 2;
        desired.Samples = 256; // ?
        desired.Callback = new PfnAudioCallback(AudioCallback);

        var actual = new AudioSpec();
        IntPtr _desired = Marshal.AllocHGlobal(Marshal.SizeOf(desired));
        IntPtr _actual = Marshal.AllocHGlobal(Marshal.SizeOf(actual));

        _ptr = _emulator.VeraAudio.PcmPtr;

        #if LOG_OUTPUT
        _writer = new StreamWriter("c:\\temp\\audio.txt");
        #endif

        try
        {
            Marshal.StructureToPtr<AudioSpec>(desired, _desired, false);
            Marshal.StructureToPtr<AudioSpec>(actual, _actual, false);

            _audio_device = _sdl.OpenAudioDevice((string?)null, 0, (AudioSpec*)_desired, (AudioSpec*)_actual, 0);

            actual = Marshal.PtrToStructure<AudioSpec>(_actual); // copy back
        }
        finally
        {
            Marshal.FreeHGlobal(_desired);
            Marshal.FreeHGlobal(_actual);
        }
    }

    public void StartPlayback()
    {
        _sdl.PauseAudioDevice(_audio_device, 0);
    }

    public void StopPlayback()
    {
        _sdl.PauseAudioDevice(_audio_device, 1);
    }

    public void Dispose()
    {
        if (_audio_device != 0)
            _sdl.CloseAudioDevice(_audio_device);

        #if LOG_OUTPUT
        _writer.Flush();
        _writer.Close();
        _writer.Dispose();
        #endif
    }

    // Buffer Sizes and indexes are in 4 byte steps!
    // SDL length is in bytes.
    public void AudioCallback(void* userdata, byte* stream, int length)
    {
        //var buffW = _emulator.AudioWrite;
        //var bufferWrite = buffW & _bufferMask;
        var bufferWrite = _emulator.AudioWrite & ~(uint)3;
        var actLength = 0x100;
        var outputOffset = 0;

        if (length != 0x400)
            throw new Exception("Audio request size missmatch!");

        if (_bufferRead == bufferWrite)
        {
            new Span<byte>(stream, length).Clear();

            return;
        }

        Delay = (bufferWrite - _bufferRead) & _bufferMask;

        if (Delay > 0xb00) // more than 11 frames behind
        {
            Console.Write($"Audio to far behind (0x{Delay:X4}) was {_bufferRead:X8}");
            _bufferRead = (bufferWrite - 0x700) & _bufferMask & ~(uint)0xff;
            Console.WriteLine($" now {_bufferRead:X8}");
        }

        bool showDebug = false;
        if (_bufferRead + 0x100 > _bufferSize)
        {
            showDebug = true;
            var toWrite = Math.Min(_bufferSize - _bufferRead, actLength);
            Console.Write($"Wrap {_bufferRead:X8} vs {bufferWrite:X8} writing {toWrite:X4}");
            
            // copy from the read position to the end of the buffer
            Buffer.MemoryCopy((void*)(_emulator.AudioOutputPtr + _bufferRead * 4), stream, toWrite * 4, toWrite * 4);

            if (toWrite == actLength)
            {
                Console.WriteLine(" all done");
                _bufferRead = 0;
                return;
            }

            actLength -= (int)toWrite;
            outputOffset = (int)toWrite;
            _bufferRead = 0;
        }

        var len = Math.Min(actLength, bufferWrite - _bufferRead);

        if (showDebug)
        {
            Console.WriteLine($" and writing {len:X4} more at {outputOffset:X2}");
        }

        Buffer.MemoryCopy((void*)(_emulator.AudioOutputPtr + _bufferRead * 4), stream + outputOffset * 4, len * 4, len * 4);
        
        #if LOG_OUTPUT
        for (var i = 0; i < len; i++)
        {
            _writer.Write(((short)(stream[i * 4] + (stream[i * 4 + 1] << 8))).ToString());
            _writer.Write(",");
            _writer.WriteLine(((short)(stream[i * 4 + 2] + (stream[i * 4 + 3] << 8))).ToString());
        }
        #endif

        _bufferRead += (uint)len;
        _bufferRead &= _bufferMask;

        if (actLength - len != 0)
        {
            _bufferRead &= ~(uint)0xff;
            Console.WriteLine($"Audio Under flow: {actLength - len}");
        }
    }
}
