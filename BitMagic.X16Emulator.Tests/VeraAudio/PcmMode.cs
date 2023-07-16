using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Vera.Audio;

[TestClass]
public class PcmMode
{
    [TestMethod]
    public async Task Set1()
    {
        var emulator = new Emulator();

        emulator.A = 0x10;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta AUDIO_CTRL
                stp",
                emulator);

        Assert.AreEqual(1u, emulator.VeraAudio.PcmMode);
    }

    [TestMethod]
    public async Task Set2()
    {
        var emulator = new Emulator();

        emulator.A = 0x20;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta AUDIO_CTRL
                stp",
                emulator);

        Assert.AreEqual(2u, emulator.VeraAudio.PcmMode);
    }

    [TestMethod]
    public async Task Set3()
    {
        var emulator = new Emulator();

        emulator.A = 0x30;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta AUDIO_CTRL
                stp",
                emulator);

        Assert.AreEqual(3u, emulator.VeraAudio.PcmMode);
    }

    [TestMethod]
    public async Task SetSideEffects()
    {
        var emulator = new Emulator();

        emulator.A = 0xcf;
        emulator.VeraAudio.PcmMode = 1;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta AUDIO_CTRL
                stp",
                emulator);

        Assert.AreEqual(0u, emulator.VeraAudio.PcmMode);
    }
}