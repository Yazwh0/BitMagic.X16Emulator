using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Vera.Audio;

[TestClass]
public class PsgPlay
{
    private const int PSG_BASE = 0x1f9c0;
    private const int FREQ_L = 0x00;
    private const int FREQ_H = 0x01;
    private const int LR_VOLUME = 0x02;
    private const int WAVE_WIDTH = 0x03;
    private const int VOICE = 0x04;

    private const int WIDTH = 63;
    private const int PULSE = 0x00;
    private const int SAWTOOTH = 0x01 << 6;
    private const int TRIANGLE = 0x02 << 6;
    private const int NOISE = 0x03 << 6;

    [TestMethod]
    public async Task Pulse_Left_Voice0()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 0 + WAVE_WIDTH;
        emulator.VeraAudio.PsgVoices[0].Frequency = 0xffff; // change every sample
        emulator.VeraAudio.PsgVoices[0].LeftRight = 0x01;
        emulator.VeraAudio.PsgVoices[0].Volume = 1;
        emulator.A = WIDTH + PULSE;
        emulator.Clock_AudioNext = 10; // enough for the sta DATA0
        emulator.X = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                .loop:
                dex
                bne loop
                stp",
                emulator);

        Assert.AreEqual(-32, emulator.AudioOutputBuffer[0]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[1]);
        Assert.AreEqual(31, emulator.AudioOutputBuffer[2]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[3]);
        Assert.AreEqual(-32, emulator.AudioOutputBuffer[4]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[5]);
        Assert.AreEqual(31, emulator.AudioOutputBuffer[6]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[7]);
    }

    [TestMethod]
    public async Task Pulse_Right_Voice0()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 0 + WAVE_WIDTH;
        emulator.VeraAudio.PsgVoices[0].Frequency = 0xffff;
        emulator.VeraAudio.PsgVoices[0].LeftRight = 0x02;
        emulator.VeraAudio.PsgVoices[0].Volume = 1;
        emulator.A = WIDTH + PULSE;
        emulator.Clock_AudioNext = 10; // enough for the sta DATA0
        emulator.X = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                .loop:
                dex
                bne loop
                stp",
                emulator);

        Assert.AreEqual(0, emulator.AudioOutputBuffer[0]);
        Assert.AreEqual(-32, emulator.AudioOutputBuffer[1]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[2]);
        Assert.AreEqual(31, emulator.AudioOutputBuffer[3]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[4]);
        Assert.AreEqual(-32, emulator.AudioOutputBuffer[5]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[6]);
        Assert.AreEqual(31, emulator.AudioOutputBuffer[7]);
    }

    [TestMethod]
    public async Task Pulse_Both_Voice0()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 0 + WAVE_WIDTH;
        emulator.VeraAudio.PsgVoices[0].Frequency = 0xffff;
        emulator.VeraAudio.PsgVoices[0].LeftRight = 0x03;
        emulator.VeraAudio.PsgVoices[0].Volume = 1;
        emulator.A = WIDTH + PULSE;
        emulator.Clock_AudioNext = 10; // enough for the sta DATA0
        emulator.X = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                .loop:
                dex
                bne loop
                stp",
                emulator);

        Assert.AreEqual(-32, emulator.AudioOutputBuffer[0]);
        Assert.AreEqual(-32, emulator.AudioOutputBuffer[1]);
        Assert.AreEqual(31, emulator.AudioOutputBuffer[2]);
        Assert.AreEqual(31, emulator.AudioOutputBuffer[3]);
        Assert.AreEqual(-32, emulator.AudioOutputBuffer[4]);
        Assert.AreEqual(-32, emulator.AudioOutputBuffer[5]);
        Assert.AreEqual(31, emulator.AudioOutputBuffer[6]);
        Assert.AreEqual(31, emulator.AudioOutputBuffer[7]);
    }

    [TestMethod]
    public async Task Pulse_Both_Volume2_Voice0()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 0 + WAVE_WIDTH;
        emulator.VeraAudio.PsgVoices[0].Frequency = 0xffff;
        emulator.VeraAudio.PsgVoices[0].LeftRight = 0x03;
        emulator.VeraAudio.PsgVoices[0].Volume = 2;
        emulator.A = WIDTH + PULSE;
        emulator.Clock_AudioNext = 10; // enough for the sta DATA0
        emulator.X = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                .loop:
                dex
                bne loop
                stp",
                emulator);

        Assert.AreEqual(-64, emulator.AudioOutputBuffer[0]);
        Assert.AreEqual(-64, emulator.AudioOutputBuffer[1]);
        Assert.AreEqual(62, emulator.AudioOutputBuffer[2]);
        Assert.AreEqual(62, emulator.AudioOutputBuffer[3]);
        Assert.AreEqual(-64, emulator.AudioOutputBuffer[4]);
        Assert.AreEqual(-64, emulator.AudioOutputBuffer[5]);
        Assert.AreEqual(62, emulator.AudioOutputBuffer[6]);
        Assert.AreEqual(62, emulator.AudioOutputBuffer[7]);
    }

    [TestMethod]
    public async Task Pulse_Both_VolumeMax_Voice0()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 0 + WAVE_WIDTH;
        emulator.VeraAudio.PsgVoices[0].Frequency = 0xffff;
        emulator.VeraAudio.PsgVoices[0].LeftRight = 0x03;
        emulator.VeraAudio.PsgVoices[0].Volume = 64;
        emulator.A = WIDTH + PULSE;
        emulator.Clock_AudioNext = 10; // enough for the sta DATA0
        emulator.X = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                .loop:
                dex
                bne loop
                stp",
                emulator);

        Assert.AreEqual(-2048, emulator.AudioOutputBuffer[0]);
        Assert.AreEqual(-2048, emulator.AudioOutputBuffer[1]);
        Assert.AreEqual(1984, emulator.AudioOutputBuffer[2]);
        Assert.AreEqual(1984, emulator.AudioOutputBuffer[3]);
        Assert.AreEqual(-2048, emulator.AudioOutputBuffer[4]);
        Assert.AreEqual(-2048, emulator.AudioOutputBuffer[5]);
        Assert.AreEqual(1984, emulator.AudioOutputBuffer[6]);
        Assert.AreEqual(1984, emulator.AudioOutputBuffer[7]);
    }

    [TestMethod]
    public async Task Saw_Left_Voice0()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 0 + WAVE_WIDTH;
        emulator.VeraAudio.PsgVoices[0].Frequency = 0x4000; // takes 8 samples to complete a cycle
        emulator.VeraAudio.PsgVoices[0].LeftRight = 0x01;
        emulator.VeraAudio.PsgVoices[0].Volume = 1;
        emulator.A = WIDTH + SAWTOOTH;
        emulator.Clock_AudioNext = 10; // enough for the sta DATA0
        emulator.X = 0xff;
        emulator.Y = 0x10;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                .loop:
                dex
                bne loop
                dey
                bne loop
                stp",
                emulator);

        Assert.AreEqual(-24, emulator.AudioOutputBuffer[0]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[1]);
        Assert.AreEqual(-16, emulator.AudioOutputBuffer[2]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[3]);
        Assert.AreEqual(-8, emulator.AudioOutputBuffer[4]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[5]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[6]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[7]);
        Assert.AreEqual(8, emulator.AudioOutputBuffer[8]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[9]);
        Assert.AreEqual(16, emulator.AudioOutputBuffer[10]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[11]);
        Assert.AreEqual(24, emulator.AudioOutputBuffer[12]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[13]);
        Assert.AreEqual(-32, emulator.AudioOutputBuffer[14]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[15]);
        Assert.AreEqual(-24, emulator.AudioOutputBuffer[16]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[17]);
    }


    [TestMethod]
    public async Task Triangle_Left_Voice0()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 0 + WAVE_WIDTH;
        emulator.VeraAudio.PsgVoices[0].Frequency = 0x4000; // takes 8 samples to complete a cycle
        emulator.VeraAudio.PsgVoices[0].LeftRight = 0x01;
        emulator.VeraAudio.PsgVoices[0].Volume = 1;
        emulator.A = WIDTH + TRIANGLE;
        emulator.Clock_AudioNext = 10; // enough for the sta DATA0
        emulator.X = 0xff;
        emulator.Y = 0x10;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                .loop:
                dex
                bne loop
                dey
                bne loop
                stp",
                emulator);

        Assert.AreEqual(-32, emulator.AudioOutputBuffer[0]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[1]);
        Assert.AreEqual(-16, emulator.AudioOutputBuffer[2]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[3]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[4]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[5]);
        Assert.AreEqual(16, emulator.AudioOutputBuffer[6]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[7]);
        Assert.AreEqual(31, emulator.AudioOutputBuffer[8]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[9]);
        Assert.AreEqual(15, emulator.AudioOutputBuffer[10]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[11]);
        Assert.AreEqual(-1, emulator.AudioOutputBuffer[12]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[13]);
        Assert.AreEqual(-17, emulator.AudioOutputBuffer[14]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[15]);
        Assert.AreEqual(-32, emulator.AudioOutputBuffer[16]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[17]);
    }

    [TestMethod]
    public async Task Noise_High_Left_Voice0()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 0 + WAVE_WIDTH;
        emulator.VeraAudio.PsgVoices[0].Frequency = 0xffff; // every sample changes
        emulator.VeraAudio.PsgVoices[0].LeftRight = 0x01;
        emulator.VeraAudio.PsgVoices[0].Volume = 1;
        emulator.A = WIDTH + NOISE;
        emulator.Clock_AudioNext = 10; // enough for the sta DATA0
        emulator.X = 0xff;
        emulator.Y = 0x10;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                .loop:
                dex
                bne loop
                dey
                bne loop
                stp",
                emulator);

        Assert.AreEqual(-32, emulator.AudioOutputBuffer[0]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[1]);
        Assert.AreEqual(13, emulator.AudioOutputBuffer[2]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[3]);
        Assert.AreEqual(30, emulator.AudioOutputBuffer[4]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[5]);
        Assert.AreEqual(-11, emulator.AudioOutputBuffer[6]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[7]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[8]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[9]);
        Assert.AreEqual(10, emulator.AudioOutputBuffer[10]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[11]);
        Assert.AreEqual(-11, emulator.AudioOutputBuffer[12]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[13]);
        Assert.AreEqual(-22, emulator.AudioOutputBuffer[14]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[15]);
        Assert.AreEqual(-14, emulator.AudioOutputBuffer[16]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[17]);
    }

    [TestMethod]
    public async Task Noise_Low_Left_Voice0()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 0 + WAVE_WIDTH;
        emulator.VeraAudio.PsgVoices[0].Frequency = 0x8009; // every other sample changes
        emulator.VeraAudio.PsgVoices[0].LeftRight = 0x01;
        emulator.VeraAudio.PsgVoices[0].Volume = 1;
        emulator.A = WIDTH + NOISE;
        emulator.Clock_AudioNext = 10; // enough for the sta DATA0
        emulator.X = 0xff;
        emulator.Y = 0x10;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                .loop:
                dex
                bne loop
                dey
                bne loop
                stp",
                emulator);

        Assert.AreEqual(-32, emulator.AudioOutputBuffer[0]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[1]);
        Assert.AreEqual(13, emulator.AudioOutputBuffer[2]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[3]);
        Assert.AreEqual(13, emulator.AudioOutputBuffer[4]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[5]);
        Assert.AreEqual(-11, emulator.AudioOutputBuffer[6]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[7]);
        Assert.AreEqual(-11, emulator.AudioOutputBuffer[8]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[9]);
        Assert.AreEqual(10, emulator.AudioOutputBuffer[10]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[11]);
        Assert.AreEqual(10, emulator.AudioOutputBuffer[12]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[13]);
        Assert.AreEqual(-22, emulator.AudioOutputBuffer[14]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[15]);
        Assert.AreEqual(-22, emulator.AudioOutputBuffer[16]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[17]);
    }

    [TestMethod]
    public async Task Pulse_Left_Voice2()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 2 + WAVE_WIDTH;
        emulator.VeraAudio.PsgVoices[2].Frequency = 0xffff; // change every sample
        emulator.VeraAudio.PsgVoices[2].LeftRight = 0x01;
        emulator.VeraAudio.PsgVoices[2].Volume = 1;
        emulator.A = WIDTH + PULSE;
        emulator.Clock_AudioNext = 10; // enough for the sta DATA0
        emulator.X = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                .loop:
                dex
                bne loop
                stp",
                emulator);

        Assert.AreEqual(-32, emulator.AudioOutputBuffer[0]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[1]);
        Assert.AreEqual(31, emulator.AudioOutputBuffer[2]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[3]);
        Assert.AreEqual(-32, emulator.AudioOutputBuffer[4]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[5]);
        Assert.AreEqual(31, emulator.AudioOutputBuffer[6]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[7]);
    }

    [TestMethod]
    public async Task Pulse_Left_Voice15()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 15 + WAVE_WIDTH;
        emulator.VeraAudio.PsgVoices[15].Frequency = 0xffff; // change every sample
        emulator.VeraAudio.PsgVoices[15].LeftRight = 0x01;
        emulator.VeraAudio.PsgVoices[15].Volume = 1;
        emulator.A = WIDTH + PULSE;
        emulator.Clock_AudioNext = 10; // enough for the sta DATA0
        emulator.X = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                .loop:
                dex
                bne loop
                stp",
                emulator);

        Assert.AreEqual(-32, emulator.AudioOutputBuffer[0]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[1]);
        Assert.AreEqual(31, emulator.AudioOutputBuffer[2]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[3]);
        Assert.AreEqual(-32, emulator.AudioOutputBuffer[4]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[5]);
        Assert.AreEqual(31, emulator.AudioOutputBuffer[6]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[7]);
    }

    [TestMethod]
    public async Task Pulse_Left_MultipleVoice()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * 0 + WAVE_WIDTH;
        emulator.Vera.Data0_Step = 4;
        emulator.VeraAudio.PsgVoices[0].Frequency = 0x1000; // change every sample
        emulator.VeraAudio.PsgVoices[0].LeftRight = 0x01;
        emulator.VeraAudio.PsgVoices[0].Volume = 64;

        emulator.VeraAudio.PsgVoices[1].Frequency = 0x1000; // change every sample
        emulator.VeraAudio.PsgVoices[1].LeftRight = 0x01;
        emulator.VeraAudio.PsgVoices[1].Volume = 64;

        emulator.VeraAudio.PsgVoices[2].Frequency = 0x1000; // change every sample
        emulator.VeraAudio.PsgVoices[2].LeftRight = 0x01;
        emulator.VeraAudio.PsgVoices[2].Volume = 64;

        emulator.VeraAudio.PsgVoices[3].Frequency = 0x1000; // change every sample
        emulator.VeraAudio.PsgVoices[3].LeftRight = 0x01;
        emulator.VeraAudio.PsgVoices[3].Volume = 64;

        emulator.A = WIDTH + PULSE;
        emulator.Clock_AudioNext = 150; // enough for the sta DATA0
        emulator.X = 0xff;
        emulator.Y = 0x20;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta DATA0
                sta DATA0
                sta DATA0
                sta DATA0
                .loop:
                dex
                bne loop
                dey
                bne loop
                stp",
                emulator);

        Assert.AreEqual(31, emulator.AudioOutputBuffer[0]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[1]);
        Assert.AreEqual(-32, emulator.AudioOutputBuffer[2]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[3]);
        Assert.AreEqual(31, emulator.AudioOutputBuffer[4]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[5]);
        Assert.AreEqual(-32, emulator.AudioOutputBuffer[6]);
        Assert.AreEqual(0, emulator.AudioOutputBuffer[7]);
    }
}