using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

[TestClass]
public class BeamBreakpointHits
{
    private const ulong Line = Emulator.BeamCyclesPerLine;
    private const ulong Frame = Emulator.BeamCyclesPerFrame;
    private const ulong VsyncClock = Emulator.BeamVsyncLine * Line; // first vsync, the clock starts on line 0
    private const ulong MaxLate = 16; // a couple of instructions

    private static readonly HashSet<int> LoopPcs = [0x811, 0x812, 0x813, 0x814];

    private const string Loop = @"
                .machine CommanderX16R40
                .org $810
                sei
        .loop:
                nop     ; $811
                nop     ; $812
                inx     ; $813
                jmp loop; $814";

    [TestMethod]
    public async Task Line_HitsEveryFrame()
    {
        using var emulator = new Emulator();
        emulator.InterruptMask = InterruptSource.None;
        emulator.SetBeamBreakpoints([Emulator.BeamOffset(0, 240)]);

        await X16TestHelper.Emulate(Loop, emulator, expectedResult: Emulator.EmulatorResult.Breakpoint);

        for (var frame = 0; frame < 10; frame++)
        {
            if (frame != 0)
                Assert.AreEqual(Emulator.EmulatorResult.Breakpoint, emulator.EmulateWithTimeout(), $"Frame {frame}");

            var target = 240 * Line + (ulong)frame * Frame;
            var message = $"Frame {frame}, clock {emulator.Clock}, target {target}";

            Assert.IsTrue((emulator.BreakpointSource & Emulator.BreakpointSourceType.Beam) != 0, message);
            Assert.IsTrue(emulator.Clock >= target && emulator.Clock - target < MaxLate, message);
            Assert.AreEqual(240, emulator.Vera.Beam_Y, message);
            Assert.IsTrue(LoopPcs.Contains(emulator.Pc), $"{message}, pc ${emulator.Pc:X4}"); // stops between instructions
        }
    }

    [TestMethod]
    public async Task CloseTogether_AcrossVsync_AllHitOnce()
    {
        using var emulator = new Emulator();
        emulator.InterruptMask = InterruptSource.None;

        // the last cycle before vsync, then the first few after it, closer together than an instruction
        uint[] offsets = [Emulator.BeamOffset(799, 479), Emulator.BeamOffset(0, 480), Emulator.BeamOffset(3, 480), Emulator.BeamOffset(6, 480), Emulator.BeamOffset(0, 300)];
        emulator.SetBeamBreakpoints(offsets);

        var targets = new List<ulong>();
        for (var frame = -1; frame < 6; frame++)
            foreach (var offset in offsets)
            {
                var target = (long)VsyncClock + frame * (long)Frame + offset;
                if (target > 0)
                    targets.Add((ulong)target);
            }

        await X16TestHelper.Emulate(Loop, emulator, expectedResult: Emulator.EmulatorResult.Breakpoint);

        // each exit covers the targets passed since the previous one, like the debugger works it out
        ulong previous = 0;
        var covered = 0;
        var exits = 0;

        while (true)
        {
            var clock = emulator.Clock;
            var message = $"Exit {exits}, clock {clock}";

            Assert.IsTrue((emulator.BreakpointSource & Emulator.BreakpointSourceType.Beam) != 0, message);
            Assert.IsTrue(LoopPcs.Contains(emulator.Pc), $"{message}, pc ${emulator.Pc:X4}");

            var hit = targets.Where(i => i > previous && i <= clock).ToList();
            Assert.IsTrue(hit.Count > 0, $"{message}, exit with no target");
            Assert.IsTrue(clock - hit.Min() < MaxLate, $"{message}, late for {hit.Min()}");

            covered += hit.Count;
            previous = clock;
            exits++;

            if (clock > VsyncClock + 4 * Frame)
                break;

            Assert.AreEqual(Emulator.EmulatorResult.Breakpoint, emulator.EmulateWithTimeout(), message);
        }

        Assert.AreEqual(targets.Count(i => i <= previous), covered, "Every target passed should be hit exactly once.");
        Assert.IsTrue(covered >= 20, $"Only {covered} targets hit.");
    }

    [TestMethod]
    public async Task EmulationUnchanged()
    {
        // counts vsync irqs, the run should end on the same clock with or without beam breakpoints
        const string program = @"
                .machine CommanderX16R40
                .org $810
                lda #01
                sta IEN
                cli
        .loop:
                lda $02
                cmp #5
                bne loop
                stp
                .org $900
                inc $02
                lda #01
                sta $9f27
                rti";

        using var plain = Create();
        await X16TestHelper.Emulate(program, plain);

        using var withBreakpoints = Create();
        withBreakpoints.SetBeamBreakpoints([Emulator.BeamOffset(799, 479), Emulator.BeamOffset(0, 480), Emulator.BeamOffset(1, 480), Emulator.BeamOffset(0, 100), Emulator.BeamOffset(400, 479)]);
        await X16TestHelper.Emulate(program, withBreakpoints, expectedResult: Emulator.EmulatorResult.Breakpoint);

        var hits = 1;
        Emulator.EmulatorResult result;
        while ((result = withBreakpoints.EmulateWithTimeout()) == Emulator.EmulatorResult.Breakpoint)
            hits++;

        Assert.AreEqual(Emulator.EmulatorResult.DebugOpCode, result);
        Assert.IsTrue(hits > 10, $"Only {hits} breakpoint hits.");

        Assert.AreEqual(5, withBreakpoints.Memory[0x02]);
        Assert.AreEqual(plain.Clock, withBreakpoints.Clock);
        Assert.AreEqual(plain.Pc, withBreakpoints.Pc);
        Assert.AreEqual(plain.Vera.Frame_Count, withBreakpoints.Vera.Frame_Count);
        Assert.AreEqual(plain.Vera.Beam_Position, withBreakpoints.Vera.Beam_Position);
    }

    private static Emulator Create()
    {
        var emulator = new Emulator(); // caller disposes

        emulator.InterruptHit = InterruptSource.None;
        emulator.InterruptMask = InterruptSource.None;

        emulator.RomBank[0x3ffe] = 0x00;
        emulator.RomBank[0x3fff] = 0x09;

        return emulator;
    }
}
