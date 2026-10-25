using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

[TestClass]
public class BeamBreakpoints
{
    private const uint Line = Emulator.BeamCyclesPerLine;

    [TestMethod]
    public void Init_TableIsEmpty()
    {
        using var emulator = new Emulator();

        foreach (var i in emulator.BeamBreakpoints)
            Assert.AreEqual(Emulator.BeamBreakpointEnd, i);

        Assert.AreEqual(Emulator.BeamBreakpointNone, emulator.Vera.Beam_Breakpoint_Next);
    }

    [TestMethod]
    public void Offset_FromVsyncLine()
    {
        Assert.AreEqual(0u, Emulator.BeamOffset(0, 480));
        Assert.AreEqual(40 * Line, Emulator.BeamOffset(0, 520));
        Assert.AreEqual(44 * Line, Emulator.BeamOffset(0, 524));
        Assert.AreEqual(45 * Line, Emulator.BeamOffset(0, 0));
        Assert.AreEqual(524 * Line, Emulator.BeamOffset(0, 479));
    }

    [TestMethod]
    public void Offset_X_IsFirstCycleAtOrAfter()
    {
        Assert.AreEqual(0u, Emulator.BeamOffset(0, 480));
        Assert.AreEqual(1u, Emulator.BeamOffset(1, 480));   // cycle 1 is dot 3.125
        Assert.AreEqual(1u, Emulator.BeamOffset(3, 480));
        Assert.AreEqual(2u, Emulator.BeamOffset(4, 480));   // cycle 2 is dot 6.25
        Assert.AreEqual(205u, Emulator.BeamOffset(640, 480)); // 640 / 3.125 = 204.8
        Assert.AreEqual(Line - 1, Emulator.BeamOffset(799, 480)); // clamped to stay on the line
        Assert.AreEqual(524 * Line + Line - 1, Emulator.BeamOffset(799, 479)); // last cycle of the frame
    }

    [TestMethod]
    public void Offset_OutOfRange_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Emulator.BeamOffset(800, 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Emulator.BeamOffset(0, 525));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Emulator.BeamOffset(-1, 0));
    }

    [TestMethod]
    public void Set_SortedFromVsync_DedupedAndPadded()
    {
        using var emulator = new Emulator();

        emulator.SetBeamBreakpoints([Emulator.BeamOffset(0, 0), Emulator.BeamOffset(0, 520), Emulator.BeamOffset(5, 481), Emulator.BeamOffset(0, 0)]);

        // vblank lines come first, as they're straight after vsync. first entry is from vsync, the rest are deltas
        Assert.AreEqual(Line + 2, emulator.BeamBreakpoints[0]);        // 481, x=5
        Assert.AreEqual(39 * Line - 2, emulator.BeamBreakpoints[1]);   // 520
        Assert.AreEqual(5 * Line, emulator.BeamBreakpoints[2]);        // 0

        for (var i = 3; i < Emulator.BeamBreakpointCount; i++)
            Assert.AreEqual(Emulator.BeamBreakpointEnd, emulator.BeamBreakpoints[i]);

        Assert.AreEqual(Emulator.BeamOffset(5, 481), emulator.GetBeamBreakpointOffset(0));
        Assert.AreEqual(Emulator.BeamOffset(0, 520), emulator.GetBeamBreakpointOffset(1));
        Assert.AreEqual(Emulator.BeamOffset(0, 0), emulator.GetBeamBreakpointOffset(2));
    }

    [TestMethod]
    public void CurrentOffset_FromClock()
    {
        using var emulator = new Emulator();

        emulator.Clock = 0;
        Assert.AreEqual(45 * Line, emulator.CurrentBeamOffset); // line 0

        emulator.Clock = 480 * Line + 10;
        Assert.AreEqual(10u, emulator.CurrentBeamOffset);

        emulator.Clock = 3 * Emulator.BeamCyclesPerFrame + 520 * Line + 7;
        Assert.AreEqual(40 * Line + 7, emulator.CurrentBeamOffset);
    }

    [TestMethod]
    public void Set_AtStart_NextIsFirstAfterLine0()
    {
        using var emulator = new Emulator();
        emulator.Clock = 0; // line 0, so the vblank entries have already gone this frame

        emulator.SetBeamBreakpoints([Emulator.BeamOffset(0, 500), Emulator.BeamOffset(0, 10), Emulator.BeamOffset(0, 240)]);

        Assert.AreEqual(1u, emulator.Vera.Beam_Breakpoint_Index);
        Assert.AreEqual(10 * Line, emulator.Vera.Beam_Breakpoint_Next);
    }

    [TestMethod]
    public void Set_MidFrame_NextIsAbsoluteClock()
    {
        using var emulator = new Emulator();
        var frameStart = 5 * (ulong)Emulator.BeamCyclesPerFrame; // line 0
        emulator.Clock = frameStart + 100 * Line + 20;

        emulator.SetBeamBreakpoints([Emulator.BeamOffset(0, 500), Emulator.BeamOffset(0, 100), Emulator.BeamOffset(320, 200)]);

        // line 100 x=0 has gone, so next is line 200
        Assert.AreEqual(2u, emulator.Vera.Beam_Breakpoint_Index);
        Assert.AreEqual(frameStart + 200 * Line + 103, emulator.Vera.Beam_Breakpoint_Next); // 320 / 3.125 = 102.4
    }

    [TestMethod]
    public void Set_InVblank_NextIsLaterInSameFrame()
    {
        using var emulator = new Emulator();
        var frameStart = 2 * (ulong)Emulator.BeamCyclesPerFrame;
        emulator.Clock = frameStart + 490 * Line; // after vsync

        emulator.SetBeamBreakpoints([Emulator.BeamOffset(0, 485), Emulator.BeamOffset(0, 520), Emulator.BeamOffset(0, 10)]);

        Assert.AreEqual(1u, emulator.Vera.Beam_Breakpoint_Index);
        Assert.AreEqual(frameStart + 520 * Line, emulator.Vera.Beam_Breakpoint_Next);
    }

    [TestMethod]
    public void Set_AllPassed_NextIsNone()
    {
        using var emulator = new Emulator();
        emulator.Clock = 479 * Line + 200; // the end of the frame, just before vsync

        emulator.SetBeamBreakpoints([Emulator.BeamOffset(0, 480), Emulator.BeamOffset(0, 240)]);

        Assert.AreEqual(2u, emulator.Vera.Beam_Breakpoint_Index);
        Assert.AreEqual(Emulator.BeamBreakpointNone, emulator.Vera.Beam_Breakpoint_Next);
    }

    [TestMethod]
    public void Set_OnTheBreakpoint_DoesntRefire()
    {
        using var emulator = new Emulator();
        emulator.Clock = 240 * Line;

        emulator.SetBeamBreakpoints([Emulator.BeamOffset(0, 240), Emulator.BeamOffset(0, 250)]);

        Assert.AreEqual(1u, emulator.Vera.Beam_Breakpoint_Index);
        Assert.AreEqual(250 * Line, emulator.Vera.Beam_Breakpoint_Next);
    }

    [TestMethod]
    public void Set_Empty_ClearsTable()
    {
        using var emulator = new Emulator();

        emulator.SetBeamBreakpoints([100, 200]);
        emulator.SetBeamBreakpoints([]);

        foreach (var i in emulator.BeamBreakpoints)
            Assert.AreEqual(Emulator.BeamBreakpointEnd, i);

        Assert.AreEqual(0u, emulator.Vera.Beam_Breakpoint_Index);
        Assert.AreEqual(Emulator.BeamBreakpointNone, emulator.Vera.Beam_Breakpoint_Next);
    }

    [TestMethod]
    public void Set_Max_Allowed()
    {
        using var emulator = new Emulator();

        emulator.SetBeamBreakpoints(Enumerable.Range(0, Emulator.BeamBreakpointCount).Select(i => (uint)i * Line));

        Assert.AreEqual(Line, emulator.BeamBreakpoints[Emulator.BeamBreakpointCount - 1]);
        Assert.AreEqual((uint)(Emulator.BeamBreakpointCount - 1) * Line, emulator.GetBeamBreakpointOffset(Emulator.BeamBreakpointCount - 1));
    }

    [TestMethod]
    public void Set_TooMany_Throws()
    {
        using var emulator = new Emulator();

        Assert.ThrowsException<ArgumentException>(() =>
            emulator.SetBeamBreakpoints(Enumerable.Range(0, Emulator.BeamBreakpointCount + 1).Select(i => (uint)i * Line)));
    }

    [TestMethod]
    public void Set_OutOfRange_Throws()
    {
        using var emulator = new Emulator();

        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            emulator.SetBeamBreakpoints([Emulator.BeamCyclesPerFrame]));
    }
}
