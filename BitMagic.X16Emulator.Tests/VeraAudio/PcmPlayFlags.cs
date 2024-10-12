using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Vera.Audio;

[TestClass]
public class PcmPlayFlags
{
    [TestMethod]
    public async Task NotEmpty_8bitMono()
    {
        var emulator = new Emulator();

        emulator.VeraAudio.PcmBufferWrite = 2;
        emulator.VeraAudio.PcmSampleRate = 0x80; // max
        emulator.VeraAudio.PcmBufferCount = 2;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                nop
                nop
                stp",
                emulator);

        Assert.AreEqual(1u, emulator.VeraAudio.PcmBufferRead);
        Assert.AreEqual((byte)0b00000000, emulator.Memory[0x9f3b]);
    }

    [TestMethod]
    public async Task Empty_8bitMono()
    {
        var emulator = new Emulator();

        emulator.VeraAudio.PcmBufferWrite = 1;
        emulator.VeraAudio.PcmSampleRate = 0x80; // max
        emulator.VeraAudio.PcmBufferCount = 1;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                nop
                nop
                stp",
                emulator);

        Assert.AreEqual(1u, emulator.VeraAudio.PcmBufferRead);
        Assert.AreEqual(0u, emulator.VeraAudio.PcmBufferCount);
        Assert.AreEqual((byte)0b01000000, emulator.Memory[0x9f3b]);
    }

    [TestMethod]
    public async Task Empty_8bitMono_Wrap()
    {
        var emulator = new Emulator();

        emulator.VeraAudio.PcmBufferWrite = 0xfff;
        emulator.VeraAudio.PcmBufferWrite = 0;
        emulator.VeraAudio.PcmSampleRate = 0x80; // max

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                nop
                nop
                stp",
                emulator);

        Assert.AreEqual(0u, emulator.VeraAudio.PcmBufferRead);
        Assert.AreEqual((byte)0b01000000, emulator.Memory[0x9f3b]);
    }
}