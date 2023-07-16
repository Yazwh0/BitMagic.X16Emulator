using Silk.NET.Core;
using Silk.NET.Core.Contexts;
using Silk.NET.SDL;
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

    public EmulatorAudio(Emulator emulator)
    {
        _bufferMask = Emulator.AudioOutputSize / 4 - 1 ;
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
    }

    // Buffer Sizes and indexes are in 4 byte steps!
    // SDL length is in bytes?
    public void AudioCallback(void* userdata, byte* stream, int length)
    {
        var bufferWrite = _emulator.AudioWrite;
        var actLength = length / 4;

//        Console.WriteLine($"Callback for {actLength} Read: {_bufferRead} Write: {bufferWrite}, Behind {bufferWrite - _bufferRead}");

        //if (bufferWrite - _bufferRead > 150)
        //{
        //    _bufferRead = bufferWrite - 0x150;
        //    _bufferRead &= _bufferMask;
        //}

        if (_bufferRead == bufferWrite)
            return;

        // check if the write has looped around or not
        if (_bufferRead > bufferWrite)
        {
            //Buffer.MemoryCopy((void*)(_emulator.VeraAudio.PcmPtr + _bufferRead * 4), stream, length, len);

            var len = Math.Min(length / 4, _bufferSize - _bufferRead + bufferWrite - 1);

            _bufferRead += (uint)len;
            _bufferRead &= _bufferMask;

            _bufferRead = 0;

            if (actLength - len != 0)
                Console.WriteLine($"Audio Under flow: {actLength - len}");
        }
        else
        {
            var len = Math.Min(length / 4, bufferWrite - _bufferRead - 1);


            Buffer.MemoryCopy((void*)(_emulator.AudioOutputPtr + _bufferRead * 4), stream, length, len * 4);

            //bool hasData = false;
            //for(var i = 0; i < len; i++)
            //{
            //    if (_emulator.AudioOutputBuffer[(int)_bufferRead+i] != 0  && _emulator.AudioOutputBuffer[(int)_bufferRead+i] != 0x1c00) {
            //        hasData = true;
            //        break;
            //    }
            //}

            _bufferRead += (uint)len;
            _bufferRead &= _bufferMask;

            //if (hasData)
            //    Console.WriteLine("Data found!");

            if (actLength - len != 0)
                Console.WriteLine($"Audio Under flow: {actLength - len}");
        }
    }
}
