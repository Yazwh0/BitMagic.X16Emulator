using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

/// <summary>
/// The display beam (rendered lazily from the clock) and the cpu loop's line counter (which drives line irq and vsync)
/// are both derived from the cpu clock. These check they stay in step over many frames, as beam breakpoints rely on it.
/// </summary>
[TestClass]
public class BeamSync
{
    private const ulong CyclesPerLine = 256;
    private const ulong Lines = 525;
    private const ulong VsyncLine = 480;
    private const ulong DotsPerFrame = 800 * 525;

    [TestMethod]
    public async Task DisplayAndCpuLines_StayInSync()
    {
        using var emulator = new Emulator();
        emulator.FrameControl = FrameControl.Run; // warp
        emulator.InterruptMask = InterruptSource.None;

        // the delay is ~1.4 frames and not a multiple of a line, so each stop lands somewhere new in the frame
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sei
        .loop:
                stp
                ldy #$91
        .y_loop:
                ldx #$ff
        .x_loop:
                dex
                bne x_loop
                dey
                bne y_loop
                jmp loop",
                emulator);

        var linesSeen = new HashSet<ulong>();

        for (var i = 0; i < 1000; i++)
        {
            var result = emulator.EmulateWithTimeout();
            Assert.AreEqual(Emulator.EmulatorResult.DebugOpCode, result, $"Stop {i}");

            CheckInSync(emulator, i);
            linesSeen.Add(emulator.Vera.Beam_Y);
        }

        Assert.IsTrue(emulator.Vera.Frame_Count > 1000, $"Only ran {emulator.Vera.Frame_Count} frames.");
        Assert.IsTrue(linesSeen.Count > 400, $"Stops only landed on {linesSeen.Count} different lines.");
        Assert.IsTrue(linesSeen.Any(i => i >= VsyncLine), "No stops landed in vblank.");
    }

    private static void CheckInSync(Emulator emulator, int stop)
    {
        var clock = emulator.Clock;
        var message = $"Stop {stop}, clock {clock}";

        // display: the beam is exactly where the clock says, 3.125 dots per cycle
        Assert.AreEqual((uint)(clock * 25 / 8 % DotsPerFrame), emulator.Vera.Beam_Position, message);

        // cpu loop: the line counter matches its own line clock, and that is this line or, if the final
        // instruction crossed a line, the one before (the line check runs after the instruction)
        var cpuLineClock = emulator.Vera.Cpu_LineClock;
        Assert.AreEqual(cpuLineClock / CyclesPerLine % Lines, emulator.Vera.Cpu_YPos, message);

        var clockLine = clock & ~(CyclesPerLine - 1);
        Assert.IsTrue(cpuLineClock == clockLine || cpuLineClock == clockLine - CyclesPerLine, $"{message}, line clock {cpuLineClock}");

        // so the display and cpu line agree, other than that one instruction
        var expectedBeamY = cpuLineClock == clockLine ? emulator.Vera.Cpu_YPos : (emulator.Vera.Cpu_YPos + 1) % Lines;
        Assert.AreEqual(expectedBeamY, (ulong)emulator.Vera.Beam_Y, message);

        // vsync has fired once for every time the cpu loop has passed line 480
        var totalLines = cpuLineClock / CyclesPerLine;
        Assert.AreEqual((uint)((totalLines + Lines - VsyncLine) / Lines), emulator.Vera.Frame_Count, message);
    }
}
