using BitMagic.X16Emulator.TestHelper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Silk.NET.Input;

namespace BitMagic.X16Emulator.Tests.Smc;

/// <summary>
/// Typematic repeat is timed in emulated frames, so only frames the emulator actually runs count towards it.
/// </summary>
[TestClass]
public class KeyRepeat
{
    private Emulator _emulator = null!;

    [TestInitialize]
    public void Setup()
    {
        _emulator = X16TestHelper.NewEmulator();

        // jmp * , so the emulator can run any number of frames
        _emulator.Memory[0x810] = 0x4c;
        _emulator.Memory[0x811] = 0x10;
        _emulator.Memory[0x812] = 0x08;
        _emulator.Pc = 0x810;
    }

    [TestCleanup]
    public void Cleanup() => _emulator.Dispose();

    private void RunFrames(uint frames)
    {
        _emulator.Vera.Frame_Count_Breakpoint = _emulator.Vera.Frame_Count + frames;
        _emulator.Emulate();
    }

    private byte[] Keys()
    {
        var toReturn = new List<byte>();
        var position = _emulator.Keyboard_ReadPosition;

        while (position != _emulator.Keyboard_WritePosition)
        {
            toReturn.Add(_emulator.KeyboardBuffer[(int)position]);
            position = (position + 1) & (Emulator.SmcKeyboardBufferSize - 1);
        }

        return toReturn.ToArray();
    }

    private byte Down(Key key) => _emulator.SmcBuffer.KeyToIbmScanCode(key);
    private byte Up(Key key) => (byte)(_emulator.SmcBuffer.KeyToIbmScanCode(key) | 0x80);

    [TestMethod]
    public void HeldUnderDelay_DoesntRepeat()
    {
        _emulator.SmcBuffer.KeyDown(Key.Enter);
        RunFrames(29);
        _emulator.SmcBuffer.TickKeyRepeat();

        CollectionAssert.AreEqual(new[] { Down(Key.Enter) }, Keys());
    }

    [TestMethod]
    public void HeldPastDelay_Repeats_ThenAtTheInterval()
    {
        _emulator.SmcBuffer.KeyDown(Key.Enter);
        RunFrames(30);
        _emulator.SmcBuffer.TickKeyRepeat();

        CollectionAssert.AreEqual(new[] { Down(Key.Enter), Down(Key.Enter) }, Keys());

        RunFrames(4);
        _emulator.SmcBuffer.TickKeyRepeat();
        Assert.AreEqual(2, Keys().Length, "Before the interval.");

        RunFrames(1);
        _emulator.SmcBuffer.TickKeyRepeat();
        Assert.AreEqual(3, Keys().Length);
    }

    // The emulator doesn't run while the debugger is paused or handling a breakpoint, so however long that takes in
    // real time a held key mustn't repeat.
    [TestMethod]
    public void EmulatorNotRunning_DoesntRepeat()
    {
        RunFrames(100);

        _emulator.SmcBuffer.KeyDown(Key.Enter);

        for (var i = 0; i < 1000; i++)
            _emulator.SmcBuffer.TickKeyRepeat();

        CollectionAssert.AreEqual(new[] { Down(Key.Enter) }, Keys());
    }

    // Only the frames since the key went down count, not how long the emulator ran before.
    [TestMethod]
    public void EmulatorRanBeforeKeyDown_DoesntRepeatEarly()
    {
        RunFrames(1000);

        _emulator.SmcBuffer.KeyDown(Key.Enter);
        RunFrames(10);
        _emulator.SmcBuffer.TickKeyRepeat();

        CollectionAssert.AreEqual(new[] { Down(Key.Enter) }, Keys());
    }

    // The window presses keys while the emulator runs on its own thread. A short press must not repeat when the
    // emulator then stops, eg for a breakpoint: the emulated clock is only written back on a stop, so timing by it saw
    // the whole run as having happened while the key was down.
    [TestMethod]
    public void ShortPressWhileRunning_ThenEmulatorStops_DoesntRepeat()
    {
        RunFrames(1);

        _emulator.FrameControl = FrameControl.Synced;
        _emulator.Vera.Frame_Count_Breakpoint = 0; // never
        var running = new Thread(() => _emulator.Emulate());
        running.Start();

        Thread.Sleep(1000);
        _emulator.SmcBuffer.KeyDown(Key.Enter);
        Thread.Sleep(100);

        _emulator.Control = Control.Stop;
        Assert.IsTrue(running.Join(TimeSpan.FromSeconds(10)), "Emulator didn't stop.");

        _emulator.SmcBuffer.TickKeyRepeat();

        CollectionAssert.AreEqual(new[] { Down(Key.Enter) }, Keys());
    }

    [TestMethod]
    public void ReleasedBeforeDelay_DoesntRepeat()
    {
        _emulator.SmcBuffer.KeyDown(Key.Enter);
        RunFrames(10);
        _emulator.SmcBuffer.KeyUp(Key.Enter);
        RunFrames(40);
        _emulator.SmcBuffer.TickKeyRepeat();

        CollectionAssert.AreEqual(new[] { Down(Key.Enter), Up(Key.Enter) }, Keys());
    }

    [TestMethod]
    public void Modifier_DoesntRepeat()
    {
        _emulator.SmcBuffer.KeyDown(Key.ShiftLeft);
        RunFrames(40);
        _emulator.SmcBuffer.TickKeyRepeat();

        CollectionAssert.AreEqual(new[] { Down(Key.ShiftLeft) }, Keys());
    }
}
