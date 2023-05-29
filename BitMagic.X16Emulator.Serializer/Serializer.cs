using Newtonsoft.Json;
using static BitMagic.X16Emulator.Emulator;

namespace BitMagic.X16Emulator.Serializer;

public static class X16EmulatorSerilizer
{
    public static string Serialize(this Emulator emulator)
    {
        var toReturn = new EmulatorState();
        toReturn.State = emulator.State;

        toReturn.Ram = emulator.Memory.ToArray();
        toReturn.BankedRam = emulator.RamBank.ToArray();
        toReturn.BankedRom = emulator.RomBank.ToArray();
        toReturn.Vram = emulator.Vera.Vram.ToArray();
        toReturn.Display = emulator.DisplayRaw.ToArray();
        toReturn.Palette = emulator.Palette.ToArray();
        toReturn.DisplayBuffer = emulator.DisplayRaw.ToArray();
        toReturn.Sprites = emulator.Sprites.ToArray();
        toReturn.I2cBuffer = emulator.I2c.Buffer.ToArray();
        toReturn.SmcKeyboard = emulator.KeyboardBuffer.ToArray();
        toReturn.SmcMouse = emulator.MouseBuffer.ToArray();
        toReturn.SpiInboundBuffer = emulator.SpiInboundBuffer.ToArray();
        toReturn.SpiOutboundBuffer = emulator.SpiOutboundBuffer.ToArray();
        toReturn.StackInfo = emulator.StackInfo.ToArray();
        toReturn.RtcNvram = emulator.RtcNvram.ToArray();

        return JsonConvert.SerializeObject(toReturn);
    }

    public static void Deserialize(this Emulator emulator, Stream data)
    {
        using StreamReader sr = new StreamReader(data);
        using JsonReader reader = new JsonTextReader(sr);
        var serializer = new JsonSerializer();

        var state = serializer.Deserialize<EmulatorState>(reader);

        if (state == null)
            throw new Exception("could not deserialize state");

        emulator.SetState(state.State);

        CopyData(state.Ram, emulator.Memory);
        CopyData(state.BankedRam, emulator.RamBank);
        CopyData(state.BankedRom, emulator.RomBank);
        CopyData(state.Vram, emulator.Vera.Vram);
        CopyData(state.Display, emulator.DisplayRaw);
        CopyData(state.Palette, emulator.Palette);
        CopyData(state.DisplayBuffer, emulator.DisplayRaw);
        CopyData(state.Sprites, emulator.Sprites);
        CopyData(state.I2cBuffer, emulator.I2c.Buffer);
        CopyData(state.SmcKeyboard, emulator.KeyboardBuffer);
        CopyData(state.SmcMouse, emulator.MouseBuffer);
        CopyData(state.SpiInboundBuffer, emulator.SpiInboundBuffer);
        CopyData(state.SpiOutboundBuffer, emulator.SpiOutboundBuffer);
        CopyData(state.StackInfo, emulator.StackInfo);
        CopyData(state.RtcNvram, emulator.RtcNvram);
    }

    private static unsafe void CopyData<T>(T[] source, Span<T> dest)
    {
        for (var i = 0; i < source.Length; i++)
        {
            dest[i] = source[i];
        }
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
    public Common.PixelRgba[] Palette { get; set; } = Array.Empty<Common.PixelRgba>();
    public byte[] DisplayBuffer { get; set; } = Array.Empty<byte>();
    public Sprite[] Sprites { get; set; } = Array.Empty<Sprite>();
    public byte[] I2cBuffer { get; set; } = Array.Empty<byte>();
    public byte[] SmcKeyboard { get; set; } = Array.Empty<byte>();
    public byte[] SmcMouse { get; set; } = Array.Empty<byte>();
    public byte[] SpiInboundBuffer { get; set; } = Array.Empty<byte>();
    public byte[] SpiOutboundBuffer { get; set; } = Array.Empty<byte>();
    public uint[] StackInfo { get; set; } = Array.Empty<uint>();
    public byte[] RtcNvram { get; set; } = Array.Empty<byte>();
}