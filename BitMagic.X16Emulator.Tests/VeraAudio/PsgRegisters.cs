using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Vera.Audio;

[TestClass]
public class PsgRegisters
{
    private const int PSG_BASE = 0x1f9c0;
    private const int FREQ_L = 0x00;
    private const int FREQ_H = 0x01;
    private const int LR_VOLUME = 0x02;
    private const int WAVE_WIDTH = 0x03;
    private const int VOICE = 0x04;

    [TestMethod]
    public async Task FrequencyL_Voice0()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 0 + FREQ_L;
        emulator.A = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(0x00ffu, emulator.VeraAudio.PsgVoices[0].Frequency);
    }

    [TestMethod]
    public async Task FrequencyH_Voice0()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 0 + FREQ_H;
        emulator.A = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(0xff00u, emulator.VeraAudio.PsgVoices[0].Frequency);
    }

    [TestMethod]
    public async Task Volume1_Voice0()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 0 + LR_VOLUME;
        emulator.A = 0x01;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(01u, emulator.VeraAudio.PsgVoices[0].Volume);
    }

    [TestMethod]
    public async Task Volume16_Voice0()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 0 + LR_VOLUME;
        emulator.A = 0x10;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(04u, emulator.VeraAudio.PsgVoices[0].Volume);
    }

    [TestMethod]
    public async Task VolumeMax_Voice0()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 0 + LR_VOLUME;
        emulator.A = 0x3f;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(63u, emulator.VeraAudio.PsgVoices[0].Volume);
    }

    [TestMethod]
    public async Task SideLeft_Voice0()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 0 + LR_VOLUME;
        emulator.A = 0x40;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(01u, emulator.VeraAudio.PsgVoices[0].LeftRight);
    }

    [TestMethod]
    public async Task SideRight_Voice0()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 0 + LR_VOLUME;
        emulator.A = 0x80;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(02u, emulator.VeraAudio.PsgVoices[0].LeftRight);
    }

    [TestMethod]
    public async Task SideBoth_Voice0()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 0 + LR_VOLUME;
        emulator.A = 0xc0;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(03u, emulator.VeraAudio.PsgVoices[0].LeftRight);
    }

    [TestMethod]
    public async Task SideVolume_Voice0()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 0 + LR_VOLUME;
        emulator.A = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(03u, emulator.VeraAudio.PsgVoices[0].LeftRight);
        Assert.AreEqual(0x3fu, emulator.VeraAudio.PsgVoices[0].Volume);
    }

    [TestMethod]
    public async Task Width_Voice0()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 0 + WAVE_WIDTH;
        emulator.A = 0x3f;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(0x3fu, emulator.VeraAudio.PsgVoices[0].Width);
    }

    [TestMethod]
    public async Task WaveformPulse_Voice0()
    {
        var emulator = new Emulator();

        emulator.VeraAudio.PsgVoices[0].Waveform = 0x01;
        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 0 + WAVE_WIDTH;
        emulator.A = 0x00;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(0x00u, emulator.VeraAudio.PsgVoices[0].Waveform);
        Assert.AreNotEqual(0x00u, emulator.VeraAudio.PsgVoices[0].GenerationPtr);
    }

    [TestMethod]
    public async Task WaveformNoise_Voice0()
    {
        var emulator = new Emulator();

        emulator.VeraAudio.PsgVoices[0].Waveform = 0x01;
        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 0 + WAVE_WIDTH;
        emulator.A = 0xc0;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(0x03u, emulator.VeraAudio.PsgVoices[0].Waveform);
        Assert.AreNotEqual(0x00u, emulator.VeraAudio.PsgVoices[0].GenerationPtr);
    }

    [TestMethod]
    public async Task FrequencyL_Voice1()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 1 + FREQ_L;
        emulator.A = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(0x00ffu, emulator.VeraAudio.PsgVoices[1].Frequency);
    }

    [TestMethod]
    public async Task FrequencyH_Voice1()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 1 + FREQ_H;
        emulator.A = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(0xff00u, emulator.VeraAudio.PsgVoices[1].Frequency);
    }

    [TestMethod]
    public async Task Volume1_Voice1()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 1 + LR_VOLUME;
        emulator.A = 0x01;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(01u, emulator.VeraAudio.PsgVoices[1].Volume);
    }

    [TestMethod]
    public async Task Volume16_Voice1()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 1 + LR_VOLUME;
        emulator.A = 0x10;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(04u, emulator.VeraAudio.PsgVoices[1].Volume);
    }

    [TestMethod]
    public async Task VolumeMax_Voice1()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 1 + LR_VOLUME;
        emulator.A = 0x3f;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(63u, emulator.VeraAudio.PsgVoices[1].Volume);
    }

    [TestMethod]
    public async Task SideLeft_Voice1()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 1 + LR_VOLUME;
        emulator.A = 0x40;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(01u, emulator.VeraAudio.PsgVoices[1].LeftRight);
    }

    [TestMethod]
    public async Task SideRight_Voice1()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 1 + LR_VOLUME;
        emulator.A = 0x80;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(02u, emulator.VeraAudio.PsgVoices[1].LeftRight);
    }

    [TestMethod]
    public async Task SideBoth_Voice1()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 1 + LR_VOLUME;
        emulator.A = 0xc0;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(03u, emulator.VeraAudio.PsgVoices[1].LeftRight);
    }

    [TestMethod]
    public async Task Width_Voice1()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 1 + WAVE_WIDTH;
        emulator.A = 0x3f;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(0x3fu, emulator.VeraAudio.PsgVoices[1].Width);
    }

    [TestMethod]
    public async Task WaveformPulse_Voice1()
    {
        var emulator = new Emulator();

        emulator.VeraAudio.PsgVoices[1].Waveform = 0x01;
        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 1 + WAVE_WIDTH;
        emulator.A = 0x00;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(0x00u, emulator.VeraAudio.PsgVoices[1].Waveform);
        Assert.AreNotEqual(0x00u, emulator.VeraAudio.PsgVoices[1].GenerationPtr);
    }

    [TestMethod]
    public async Task WaveformNoise_Voice1()
    {
        var emulator = new Emulator();

        emulator.VeraAudio.PsgVoices[1].Waveform = 0x01;
        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 1 + WAVE_WIDTH;
        emulator.A = 0xc0;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(0x03u, emulator.VeraAudio.PsgVoices[1].Waveform);
        Assert.AreNotEqual(0x00u, emulator.VeraAudio.PsgVoices[1].GenerationPtr);
    }

    [TestMethod]
    public async Task FrequencyL_Voice15()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 15 + FREQ_L;
        emulator.A = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(0x00ffu, emulator.VeraAudio.PsgVoices[15].Frequency);
    }

    [TestMethod]
    public async Task FrequencyH_Voice15()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 15 + FREQ_H;
        emulator.A = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(0xff00u, emulator.VeraAudio.PsgVoices[15].Frequency);
    }

    [TestMethod]
    public async Task Volume1_Voice15()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 15 + LR_VOLUME;
        emulator.A = 0x01;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(01u, emulator.VeraAudio.PsgVoices[15].Volume);
    }

    [TestMethod]
    public async Task Volume16_Voice15()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 15 + LR_VOLUME;
        emulator.A = 0x10;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(04u, emulator.VeraAudio.PsgVoices[15].Volume);
    }

    [TestMethod]
    public async Task VolumeMax_Voice15()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 15 + LR_VOLUME;
        emulator.A = 0x3f;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(63u, emulator.VeraAudio.PsgVoices[15].Volume);
    }

    [TestMethod]
    public async Task SideLeft_Voice15()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 15 + LR_VOLUME;
        emulator.A = 0x40;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(01u, emulator.VeraAudio.PsgVoices[15].LeftRight);
    }

    [TestMethod]
    public async Task SideRight_Voice15()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 15 + LR_VOLUME;
        emulator.A = 0x80;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(02u, emulator.VeraAudio.PsgVoices[15].LeftRight);
    }

    [TestMethod]
    public async Task SideBoth_Voice15()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 15 + LR_VOLUME;
        emulator.A = 0xc0;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(03u, emulator.VeraAudio.PsgVoices[15].LeftRight);
    }

    [TestMethod]
    public async Task Width_Voice15()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 15 + WAVE_WIDTH;
        emulator.A = 0x3f;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(0x3fu, emulator.VeraAudio.PsgVoices[15].Width);
    }

    [TestMethod]
    public async Task WaveformPulse_Voice15()
    {
        var emulator = new Emulator();

        emulator.VeraAudio.PsgVoices[15].Waveform = 0x01;
        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 15 + WAVE_WIDTH;
        emulator.A = 0x00;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(0x00u, emulator.VeraAudio.PsgVoices[15].Waveform);
        Assert.AreNotEqual(0x00u, emulator.VeraAudio.PsgVoices[15].GenerationPtr);
    }

    [TestMethod]
    public async Task WaveformNoise_Voice15()
    {
        var emulator = new Emulator();

        emulator.VeraAudio.PsgVoices[0].Waveform = 0x01;
        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 15 + WAVE_WIDTH;
        emulator.A = 0xc0;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                stp",
                emulator);

        Assert.AreEqual(0x03u, emulator.VeraAudio.PsgVoices[15].Waveform);
        Assert.AreNotEqual(0x00u, emulator.VeraAudio.PsgVoices[15].GenerationPtr);
    }
}