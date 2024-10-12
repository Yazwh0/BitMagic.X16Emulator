using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Vera.Audio;

[TestClass]
public class PcmData
{
    public const int AUDIO_CTRL = 0x9F3B;

    [TestMethod]
    public async Task Add()
    {
        var emulator = new Emulator();

        emulator.A = 0xab;
        emulator.Clock_AudioNext = 10000;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stz AUDIO_RATE
                sta AUDIO_DATA
                stp",
                emulator);

        Assert.AreEqual(1u, emulator.VeraAudio.PcmBufferWrite); // one write
        Assert.AreEqual(0u, emulator.VeraAudio.PcmBufferRead); // not yet read
        Assert.AreEqual(0xab, emulator.VeraAudio.PcmBuffer[0]);
        Assert.AreEqual(0x00, emulator.Memory[AUDIO_CTRL]); 
    }

    [TestMethod]
    public async Task Add2()
    {
        var emulator = new Emulator();

        emulator.Clock_AudioNext = 10000;
        emulator.A = 0xab;
        emulator.X = 0x12;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stz AUDIO_RATE
                sta AUDIO_DATA
                stx AUDIO_DATA
                stp",
                emulator);

        Assert.AreEqual(2u, emulator.VeraAudio.PcmBufferWrite); // one write
        Assert.AreEqual(0u, emulator.VeraAudio.PcmBufferRead); // not yet read
        Assert.AreEqual(0xab, emulator.VeraAudio.PcmBuffer[0]);
        Assert.AreEqual(0x12, emulator.VeraAudio.PcmBuffer[1]);
        Assert.AreEqual(0x00, emulator.Memory[AUDIO_CTRL]);
    }

    [TestMethod]
    public async Task AddWrap()
    {
        var emulator = new Emulator();

        emulator.VeraAudio.PcmBufferWrite = 4096 - 2;
        emulator.VeraAudio.PcmBufferRead = 4090;
        emulator.Clock_AudioNext = 10000;
        emulator.A = 0xab;
        emulator.X = 0x12;
        emulator.Y = 0x34;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stz AUDIO_RATE
                sta AUDIO_DATA
                stx AUDIO_DATA
                sty AUDIO_DATA
                stp",
                emulator);

        Assert.AreEqual(1u, emulator.VeraAudio.PcmBufferWrite); // three writes, and wrapped
        Assert.AreEqual(4090u, emulator.VeraAudio.PcmBufferRead); // not yet read
        Assert.AreEqual(0xab, emulator.VeraAudio.PcmBuffer[4094]);
        Assert.AreEqual(0x12, emulator.VeraAudio.PcmBuffer[4095]);
        Assert.AreEqual(0x34, emulator.VeraAudio.PcmBuffer[0]);
        Assert.AreEqual(0x00, emulator.Memory[AUDIO_CTRL]);
    }

    [TestMethod]
    public async Task AddWhenFull()
    {
        var emulator = new Emulator();

        emulator.A = 0xab;
        emulator.VeraAudio.PcmBufferRead = 1;
        emulator.VeraAudio.PcmBufferCount = 4096 - 1;
        emulator.Clock_AudioNext = 10000;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stz AUDIO_RATE
                sta AUDIO_DATA
                stp",
                emulator);

        Assert.AreEqual(0u, emulator.VeraAudio.PcmBufferWrite); // one write
        Assert.AreEqual(1u, emulator.VeraAudio.PcmBufferRead); // not yet read
        Assert.AreEqual(0x00, emulator.VeraAudio.PcmBuffer[0]); // nothing should be written
        Assert.AreEqual(0x00, emulator.VeraAudio.PcmBuffer[1]); // nothing should be written
        Assert.AreEqual(0x00, emulator.VeraAudio.PcmBuffer[2]); // nothing should be written
        Assert.AreEqual(0x80, emulator.Memory[AUDIO_CTRL]);
    }

    [TestMethod]
    public async Task AddThenFull()
    {
        var emulator = new Emulator();

        emulator.A = 0xab;
        emulator.VeraAudio.PcmBufferRead = 2;
        emulator.VeraAudio.PcmBufferCount = 4096 - 2;
        emulator.Clock_AudioNext = 10000;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stz AUDIO_RATE
                sta AUDIO_DATA
                stp",
                emulator);

        Assert.AreEqual(1u, emulator.VeraAudio.PcmBufferWrite); // one write
        Assert.AreEqual(2u, emulator.VeraAudio.PcmBufferRead); // not yet read
        Assert.AreEqual(0xab, emulator.VeraAudio.PcmBuffer[0]); // we write
        Assert.AreEqual(0x00, emulator.VeraAudio.PcmBuffer[1]); // nothing should be written
        Assert.AreEqual(0x00, emulator.VeraAudio.PcmBuffer[2]);
        Assert.AreEqual(0x80, emulator.Memory[AUDIO_CTRL]);
    }

    [TestMethod]
    public async Task EmptyOnstartup()
    {
        var emulator = new Emulator();
        emulator.Clock_AudioNext = 10000;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp",
                emulator);

        Assert.AreEqual(0x40, emulator.Memory[AUDIO_CTRL]);
    }

    [TestMethod]
    public async Task FullOnstartup()
    {
        var emulator = new Emulator();

        emulator.VeraAudio.PcmBufferRead = 1;
        emulator.Clock_AudioNext = 10000;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp",
                emulator);

        Assert.AreEqual(0x80, emulator.Memory[AUDIO_CTRL]);
    }

    [TestMethod]
    public async Task FullOnstartupWrap()
    {
        var emulator = new Emulator();

        emulator.VeraAudio.PcmBufferRead = 0;
        emulator.VeraAudio.PcmBufferWrite = 0xfff;
        emulator.Clock_AudioNext = 10000;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp",
                emulator);

        Assert.AreEqual(0x80, emulator.Memory[AUDIO_CTRL]);
    }
}