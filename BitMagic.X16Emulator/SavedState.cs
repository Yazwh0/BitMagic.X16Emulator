using static BitMagic.X16Emulator.Emulator;

namespace BitMagic.X16Emulator;

/// <summary>
/// A copy of the machine taken by Emulator.SaveState, put back with Emulator.RestoreState. Held in memory only, and only
/// valid for the emulator it came from. Doesn't include the ROM, the SD card, the YM2151, the modem or anything the
/// debugger owns, such as breakpoints.
/// </summary>
public class SavedState
{
    internal CpuState State { get; init; }

    // each native buffer as raw bytes, in the order Emulator.SavedBuffers lists them
    internal byte[][] Buffers { get; init; } = [];

    public ulong Clock => State.Clock;
    public uint Frame => State.Frame_Count;
    public ushort Pc => State.Pc;

    public long Size => Buffers.Sum(i => (long)i.Length);
}
