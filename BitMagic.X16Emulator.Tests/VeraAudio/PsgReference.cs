namespace BitMagic.X16Emulator.Tests.Vera.Audio;

/// <summary>
/// The PSG as the official emulator renders it: a port of render() in src/vera_psg.c of X16Community/x16-emulator, so
/// the core can be checked against it sample by sample. One call to Render is one stereo sample.
/// </summary>
internal sealed class PsgReference
{
    public const int Pulse = 0;
    public const int Sawtooth = 1;
    public const int Triangle = 2;
    public const int Noise = 3;

    public sealed class Voice
    {
        public uint Phase;
        public ushort Frequency;
        public int Volume;          // after the volume table, eg 1 to 511
        public bool Left;
        public bool Right;
        public int Width;           // 0 to 63
        public int Waveform;
        public int NoiseValue;
    }

    public Voice[] Voices { get; } = Enumerable.Range(0, 16).Select(_ => new Voice()).ToArray();

    private ushort _noiseState = 1;

    public (short Left, short Right) Render()
    {
        short l = 0;
        short r = 0;

        foreach (var ch in Voices)
        {
            _noiseState = (ushort)((_noiseState << 1) | (((_noiseState >> 1) ^ (_noiseState >> 2) ^ (_noiseState >> 4) ^ (_noiseState >> 15)) & 1));

            var newPhase = (ch.Left || ch.Right) ? ((ch.Phase + ch.Frequency) & 0x1ffff) : 0;
            if ((ch.Phase & 0x10000) != 0 && (newPhase & 0x10000) == 0)
                ch.NoiseValue = (_noiseState >> 1) & 0x3f;
            ch.Phase = newPhase;

            var width = (uint)((ch.Width ^ 0x3f) & 0x3f);
            var v = ch.Waveform switch
            {
                Pulse => (ch.Phase >> 10) > ch.Width ? 0u : 0x3fu,
                Sawtooth => (ch.Phase >> 11) ^ width,
                Triangle => ((ch.Phase & 0x10000) != 0 ? (~(ch.Phase >> 10) & 0x3f) : ((ch.Phase >> 10) & 0x3f)) ^ width,
                _ => (uint)ch.NoiseValue
            };

            var sv = (short)(v ^ 0x20);
            if ((sv & 0x20) != 0)
                sv = (short)(sv | unchecked((short)0xffc0));

            var val = (short)(sv * ch.Volume);

            if (ch.Left)
                l += (short)(val >> 3);
            if (ch.Right)
                r += (short)(val >> 3);
        }

        return (l, r);
    }
}
