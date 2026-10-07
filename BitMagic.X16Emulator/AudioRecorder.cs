using System.Diagnostics;

namespace BitMagic.X16Emulator;

// Records the emulator's audio output to a 16-bit stereo WAV file.
//
// Reads the core's output ring buffer (AudioOutputPtr / AudioWrite) directly rather than tapping
// the SDL device, so the file holds every sample the emulator generated: MuteAudio has no effect
// (that only silences the SDL stream), SDL's catch-up / underflow handling is bypassed, and
// nothing is written while the emulator isn't running because AudioWrite doesn't move.
//
// The ring buffer holds ~10.7s of audio. AudioWrite is only a wrapping index, so if the recorder
// thread ever falls further behind than that the lap can't be detected - at a 20ms poll this
// only matters at extreme unthrottled speeds.
public sealed class AudioRecorder : IDisposable
{
    public const int SampleRate = 25_000_000 / 512; // 48828.125Hz, WAV needs an integer
    private const int Channels = 2;
    private const int BitsPerSample = 16;
    private const int BytesPerFrame = Channels * BitsPerSample / 8;
    private const int HeaderSize = 44;
    private const uint BufferFrames = Emulator.AudioOutputSize / BytesPerFrame;
    private const long MaxDataSize = uint.MaxValue - HeaderSize;

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan HeaderUpdateInterval = TimeSpan.FromSeconds(1);

    private readonly Emulator _emulator;
    private readonly FileStream _stream;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _stopEvent = new();
    private readonly object _stopLock = new();
    private uint _read;
    private long _frames;
    private bool _stopped;

    public string Path { get; }
    public long Frames => Interlocked.Read(ref _frames);
    public double Seconds => Frames / (double)SampleRate;

    // Set if the recorder thread failed (eg disk full); recording stops at that point.
    public Exception? Error { get; private set; }

    public AudioRecorder(Emulator emulator, string path)
    {
        _emulator = emulator;
        Path = path;

        _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        WriteHeader();

        // Only record from now, not whatever is already in the ring buffer.
        _read = _emulator.AudioWrite;

        _thread = new Thread(Run) { Name = "Audio Recorder", IsBackground = true };
        _thread.Start();
    }

    // Stops recording, flushes any remaining samples and finalises the WAV header.
    // Must be called before the emulator is disposed, as the thread reads its native buffer.
    public void Stop()
    {
        lock (_stopLock)
        {
            if (_stopped)
                return;
            _stopped = true;

            _stopEvent.Set();
            _thread.Join();

            try
            {
                WriteHeader();
            }
            catch (Exception e)
            {
                Error ??= e;
            }

            _stream.Dispose();
            _stopEvent.Dispose();
        }
    }

    public void Dispose() => Stop();

    private void Run()
    {
        try
        {
            var sinceHeader = Stopwatch.StartNew();

            while (!_stopEvent.Wait(PollInterval))
            {
                Drain();

                // Keep the sizes current so the file is still playable if X16D dies mid-recording.
                if (sinceHeader.Elapsed >= HeaderUpdateInterval)
                {
                    WriteHeader();
                    sinceHeader.Restart();
                }
            }

            Drain();
        }
        catch (Exception e)
        {
            Error = e;
        }
    }

    private unsafe void Drain()
    {
        var write = _emulator.AudioWrite;

        while (_read != write)
        {
            var end = write > _read ? write : BufferFrames; // up to the write position or the wrap
            var count = end - _read;

            if (DataSize + count * BytesPerFrame > MaxDataSize)
                throw new IOException("WAV file size limit reached.");

            _stream.Write(new ReadOnlySpan<byte>((void*)(_emulator.AudioOutputPtr + _read * BytesPerFrame), (int)(count * BytesPerFrame)));
            Interlocked.Add(ref _frames, count);

            _read = end % BufferFrames;
        }
    }

    private long DataSize => Frames * BytesPerFrame;

    private void WriteHeader()
    {
        var dataSize = (uint)DataSize;
        Span<byte> header = stackalloc byte[HeaderSize];

        "RIFF"u8.CopyTo(header);
        BitConverter.TryWriteBytes(header[4..], dataSize + HeaderSize - 8);
        "WAVE"u8.CopyTo(header[8..]);
        "fmt "u8.CopyTo(header[12..]);
        BitConverter.TryWriteBytes(header[16..], 16);                           // fmt chunk size
        BitConverter.TryWriteBytes(header[20..], (ushort)1);                    // PCM
        BitConverter.TryWriteBytes(header[22..], (ushort)Channels);
        BitConverter.TryWriteBytes(header[24..], SampleRate);
        BitConverter.TryWriteBytes(header[28..], SampleRate * BytesPerFrame);   // byte rate
        BitConverter.TryWriteBytes(header[32..], (ushort)BytesPerFrame);        // block align
        BitConverter.TryWriteBytes(header[34..], (ushort)BitsPerSample);
        "data"u8.CopyTo(header[36..]);
        BitConverter.TryWriteBytes(header[40..], dataSize);

        var position = _stream.Position;
        _stream.Position = 0;
        _stream.Write(header);
        _stream.Position = Math.Max(position, HeaderSize);
        _stream.Flush();
    }
}
