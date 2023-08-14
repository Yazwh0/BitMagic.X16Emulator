using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Vera.Audio;

[TestClass]
public class PcmVolume
{
    [TestMethod]
    public async Task Set0()
    {
        var emulator = new Emulator();

        emulator.A = 0x00;
        emulator.VeraAudio.PcmVolume = 1;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta AUDIO_CTRL
                stp",
                emulator);

        Assert.AreEqual(0u, emulator.VeraAudio.PcmVolume);
    }

    [TestMethod]
    public async Task Set1()
    {
        var emulator = new Emulator();

        emulator.A = 0x01;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta AUDIO_CTRL
                stp",
                emulator);

        Assert.AreEqual(2u, emulator.VeraAudio.PcmVolume);
    }

    [TestMethod]
    public async Task Set15()
    {
        var emulator = new Emulator();

        emulator.A = 0x0f;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta AUDIO_CTRL
                stp",
                emulator);

        Assert.AreEqual(128u, emulator.VeraAudio.PcmVolume);
    }

    [TestMethod]
    public async Task SetSideeffects()
    {
        var emulator = new Emulator();

        emulator.A = 0xf1;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta AUDIO_CTRL
                stp",
                emulator);

        Assert.AreEqual(2u, emulator.VeraAudio.PcmVolume);
    }
}