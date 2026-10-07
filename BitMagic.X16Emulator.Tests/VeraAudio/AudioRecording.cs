using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Vera.Audio;

[TestClass]
public class AudioRecording
{
    private const int PSG_BASE = 0x1f9c0;
    private const int WAVE_WIDTH = 0x03;
    private const int WIDTH = 63;
    private const int SAWTOOTH = 0x01 << 6;

    [TestMethod]
    public async Task Records_Emulated_Output_To_Wav()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Address = PSG_BASE + WAVE_WIDTH;
        emulator.VeraAudio.PsgVoices[0].Frequency = 0x4000;
        emulator.VeraAudio.PsgVoices[0].LeftRight = 0x03;
        emulator.VeraAudio.PsgVoices[0].Volume = 64;
        emulator.A = WIDTH + SAWTOOTH;
        emulator.Clock_AudioNext = 10; // enough for the sta DATA0
        emulator.X = 0xff;
        emulator.Y = 0x10;
        emulator.MuteAudio = true; // must not affect the recording

        var path = Path.Combine(Path.GetTempPath(), $"bm_audio_{Guid.NewGuid():N}.wav");

        try
        {
            var start = emulator.AudioWrite;
            var recorder = new AudioRecorder(emulator, path);

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

            var expectedFrames = emulator.AudioWrite - start;
            Assert.IsTrue(expectedFrames > 0);

            // Emulator is stopped, so nothing more should be written.
            await Task.Delay(100);

            recorder.Stop();

            Assert.IsNull(recorder.Error);
            Assert.AreEqual((long)expectedFrames, recorder.Frames);

            var wav = File.ReadAllBytes(path);
            Assert.AreEqual(44 + expectedFrames * 4, (uint)wav.Length);
            Assert.AreEqual("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
            Assert.AreEqual((uint)wav.Length - 8, BitConverter.ToUInt32(wav, 4));
            Assert.AreEqual("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
            Assert.AreEqual((ushort)1, BitConverter.ToUInt16(wav, 20));   // PCM
            Assert.AreEqual((ushort)2, BitConverter.ToUInt16(wav, 22));   // stereo
            Assert.AreEqual(AudioRecorder.SampleRate, BitConverter.ToInt32(wav, 24));
            Assert.AreEqual((ushort)16, BitConverter.ToUInt16(wav, 34));
            Assert.AreEqual("data", System.Text.Encoding.ASCII.GetString(wav, 36, 4));
            Assert.AreEqual(expectedFrames * 4, BitConverter.ToUInt32(wav, 40));

            var output = emulator.AudioOutputBuffer;
            var hasSound = false;
            for (var i = 0; i < expectedFrames * 2; i++)
            {
                var sample = BitConverter.ToInt16(wav, 44 + i * 2);
                Assert.AreEqual(output[(int)start * 2 + i], sample, $"sample {i}");
                hasSound |= sample != 0;
            }

            Assert.IsTrue(hasSound);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
