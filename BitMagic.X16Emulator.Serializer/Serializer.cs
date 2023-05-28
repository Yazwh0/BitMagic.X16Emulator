using Newtonsoft.Json;
using static BitMagic.X16Emulator.Emulator;

namespace BitMagic.X16Emulator.Serializer;

public static class X16EmulatorSerilizer
{
    public static string Serialize(this Emulator emulator)
    {
        var toReturn = new EmulatorState();
        toReturn.State = emulator.State;

        return JsonConvert.SerializeObject(toReturn);
    }
}

internal class EmulatorState
{
    public CpuState State { get; set; }
    public byte[] Ram { get; set; } = Array.Empty<byte>();
    public byte[] BankedRom { get; set; } = Array.Empty<byte>();
    public byte[] BankedRam { get; set; } = Array.Empty<byte>();
    public byte[] Vram { get; set; } = Array.Empty<byte>();
    public byte[] Display { get; set; } = Array.Empty<byte>();
    public byte[] Palette { get; set; } = Array.Empty<byte>();
    public byte[] DisplayBuffer { get; set; } = Array.Empty<byte>();
    public byte[] Sprites { get; set; } = Array.Empty<byte>();
    public byte[] I2cBuffer { get; set; } = Array.Empty<byte>();
    public byte[] SmcKeyboard { get; set; } = Array.Empty<byte>();
    public byte[] SmcMouse { get; set; } = Array.Empty<byte>();
    public byte[] SpiInboundBuffer { get; set; } = Array.Empty<byte>();
    public byte[] SpiOutboundBuffer { get; set; } = Array.Empty<byte>();
    public byte[] Breakpoints { get; set; } = Array.Empty<byte>();
    public byte[] StackInfo { get; set; } = Array.Empty<byte>();
    public byte[] StackBreakpoint { get; set; } = Array.Empty<byte>();
    public byte[] RtcNvram { get; set; } = Array.Empty<byte>();
}