using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Vera.Audio;

[TestClass]
public class PcmPlay
{
    [TestMethod]
    public async Task DoNothing()
    {
        var emulator = new Emulator();

        emulator.VeraAudio.PcmBufferWrite = 1;
        emulator.VeraAudio.PcmSampleRate = 0x0; // do nothing
        emulator.VeraAudio.PcmBuffer[0] = 0xab;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                nop
                nop
                stp",
                emulator);

        Assert.AreEqual(0x00, emulator.AudioOutputBuffer[0]);
        Assert.AreEqual(0x00, emulator.AudioOutputBuffer[1]);
    }

    [TestMethod]
    public async Task Play_8bitMono()
    {
        var emulator = new Emulator();

        emulator.VeraAudio.PcmBufferWrite = 1;
        emulator.VeraAudio.PcmSampleRate = 0x80; // max
        emulator.VeraAudio.PcmVolume = 0x01;
        emulator.VeraAudio.PcmBuffer[0] = 0xab;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                nop
                nop
                stp",
                emulator);

        Assert.AreEqual(0xab * 2, emulator.AudioOutputBuffer[0]);
        Assert.AreEqual(0xab * 2, emulator.AudioOutputBuffer[1]);
    }

    [TestMethod]
    public async Task Play_8bitMono_16Volume()
    {
        var emulator = new Emulator();

        emulator.VeraAudio.PcmBufferWrite = 1;
        emulator.VeraAudio.PcmSampleRate = 0x80; // max
        emulator.VeraAudio.PcmVolume = 128;    // max volume
        emulator.VeraAudio.PcmBuffer[0] = 0xab;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                nop
                nop
                stp",
                emulator);

        // ((0xab << 8) * 128) >> 7 = 0xab00
        Assert.AreEqual(unchecked((short)0xab00), emulator.AudioOutputBuffer[0]);
        Assert.AreEqual(unchecked((short)0xab00), emulator.AudioOutputBuffer[1]);
    }

    [TestMethod]
    public async Task AddThenPlay()
    {
        var emulator = new Emulator();

        emulator.VeraAudio.PcmBufferWrite = 0;
        emulator.VeraAudio.PcmSampleRate = 0x80; // max
        emulator.VeraAudio.PcmVolume = 128;    // max volume
        emulator.A = 0xab;
        emulator.Clock_AudioNext = 10;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta AUDIO_DATA
                nop
                nop
                nop
                nop
                nop
                nop
                stp",
                emulator);

        Assert.AreEqual(unchecked((short)0xab00), emulator.AudioOutputBuffer[0]);
        Assert.AreEqual(unchecked((short)0xab00), emulator.AudioOutputBuffer[1]);
    }
}