using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Vera.Audio;

// The core's PSG output, compared sample by sample with PsgReference, a port of the official emulator's render().
[TestClass]
public class PsgPlay
{
    private const int PSG_BASE = 0x1f9c0;
    private const int WAVE_WIDTH = 0x03;
    private const int VOICE = 0x04;

    private const int WIDTH = 63;

    // stereo samples compared, the program below runs for about 120
    private const int Samples = 64;

    /// <summary>
    /// Sets up a voice, writes its waveform and width with the CPU (as a program would), runs for a while, then checks
    /// the first samples against the reference.
    /// </summary>
    private static async Task Play(int voice, ushort frequency, uint leftRight, uint volume, int waveform, int width = WIDTH)
    {
        using var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + VOICE * voice + WAVE_WIDTH;
        emulator.VeraAudio.PsgVoices[voice].Frequency = frequency;
        emulator.VeraAudio.PsgVoices[voice].LeftRight = leftRight;
        emulator.VeraAudio.PsgVoices[voice].Volume = volume;
        emulator.A = (byte)(width + (waveform << 6));
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

        var reference = new PsgReference();
        var ch = reference.Voices[voice];
        ch.Frequency = frequency;
        ch.Left = (leftRight & 1) != 0;
        ch.Right = (leftRight & 2) != 0;
        ch.Volume = (int)volume;
        ch.Width = width;
        ch.Waveform = waveform;

        var heard = false;
        for (var i = 0; i < Samples; i++)
        {
            var (left, right) = reference.Render();
            Assert.AreEqual(left, emulator.AudioOutputBuffer[i * 2], $"Left, sample {i}.");
            Assert.AreEqual(right, emulator.AudioOutputBuffer[i * 2 + 1], $"Right, sample {i}.");
            heard |= left != 0 || right != 0;
        }

        Assert.IsTrue(heard, "No sound, so the comparison proves nothing.");
    }

    // the reference against values worked out by hand from the official code: volume 1, width 63, from phase 0
    [TestMethod]
    [DataRow(PsgReference.Pulse, 0xffff, new short[] { 3, -4, 3, -4 })]
    [DataRow(PsgReference.Sawtooth, 0x4000, new short[] { -3, -2, -1, 0, 1, 2, 3, -4 })]
    [DataRow(PsgReference.Triangle, 0x4000, new short[] { -2, 0, 2, 3, 1, -1, -3, -4 })]
    public void Reference(int waveform, int frequency, short[] expected)
    {
        var reference = new PsgReference();
        var ch = reference.Voices[0];
        ch.Frequency = (ushort)frequency;
        ch.Left = true;
        ch.Volume = 1;
        ch.Width = WIDTH;
        ch.Waveform = waveform;

        CollectionAssert.AreEqual(expected, expected.Select(_ => reference.Render().Left).ToArray());
    }

    // $ffff moves the pulse on every sample
    [TestMethod]
    public async Task Pulse_Left_Voice0() => await Play(0, 0xffff, 0x01, 1, PsgReference.Pulse);

    [TestMethod]
    public async Task Pulse_Right_Voice0() => await Play(0, 0xffff, 0x02, 1, PsgReference.Pulse);

    [TestMethod]
    public async Task Pulse_Both_Voice0() => await Play(0, 0xffff, 0x03, 1, PsgReference.Pulse);

    [TestMethod]
    public async Task Pulse_Both_Volume2_Voice0() => await Play(0, 0xffff, 0x03, 2, PsgReference.Pulse);

    [TestMethod]
    public async Task Pulse_Both_VolumeMax_Voice0() => await Play(0, 0xffff, 0x03, 511, PsgReference.Pulse);

    [TestMethod]
    public async Task Pulse_Left_Voice2() => await Play(2, 0xffff, 0x01, 1, PsgReference.Pulse);

    [TestMethod]
    public async Task Pulse_Left_Voice15() => await Play(15, 0xffff, 0x01, 1, PsgReference.Pulse);

    [TestMethod]
    public async Task Pulse_Left_HalfWidth() => await Play(0, 0x1000, 0x01, 1, PsgReference.Pulse, 32);

    // $4000 takes 8 samples to complete a cycle
    [TestMethod]
    public async Task Saw_Left_Voice0() => await Play(0, 0x4000, 0x01, 1, PsgReference.Sawtooth);

    [TestMethod]
    public async Task Saw_Left_Width32() => await Play(0, 0x1000, 0x01, 1, PsgReference.Sawtooth, 32);

    [TestMethod]
    public async Task Triangle_Left_Voice0() => await Play(0, 0x4000, 0x01, 1, PsgReference.Triangle);

    [TestMethod]
    public async Task Triangle_Left_Width32() => await Play(0, 0x1000, 0x01, 1, PsgReference.Triangle, 32);

    // a new noise value is taken each time bit 16 of the phase falls
    [TestMethod]
    public async Task Noise_High_Left_Voice0() => await Play(0, 0xffff, 0x01, 1, PsgReference.Noise);

    [TestMethod]
    public async Task Noise_Low_Left_Voice0() => await Play(0, 0x8009, 0x01, 1, PsgReference.Noise);

    // the width doesn't change noise
    [TestMethod]
    public async Task Noise_Left_Width0() => await Play(0, 0xffff, 0x01, 1, PsgReference.Noise, 0);
}
