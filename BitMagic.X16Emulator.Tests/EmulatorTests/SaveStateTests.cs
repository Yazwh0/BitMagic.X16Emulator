using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

[TestClass]
public class SaveStateTests
{
    // counts vsync irqs in $02 while writing ram, banked ram and vram, stops at the breakpoint on $810 each time round
    private const string Program = @"
                .machine CommanderX16R40
                .org $810
                nop
                lda #01
                sta IEN
                lda #$10
                sta ADDRx_H
                cli
        .loop:
                inc $1000
                lda $1000
                sta DATA0
                ldx #3
                stx RAM_BANK
                sta $a000
                ldy #0
        .delay:
                dey
                bne delay
                lda $02
                cmp #50
                bne loop
                stp
                .org $900
                inc $02
                lda #01
                sta ISR
                rti";

    private const int Loop = 0x81c; // .loop, after the 12 bytes of setup

    [TestMethod]
    public async Task RestoreState_PutsMachineBack()
    {
        using var emulator = Create();
        emulator.Breakpoints[Loop] = 1;

        await X16TestHelper.Emulate(Program, emulator, expectedResult: Emulator.EmulatorResult.Breakpoint);
        RunHits(emulator, 20);

        var saved = emulator.SaveState();
        var (clock, pc, a, x, y, sp) = (emulator.Clock, emulator.Pc, emulator.A, emulator.X, emulator.Y, emulator.StackPointer);
        var memory = emulator.Memory.ToArray();
        var ramBank = emulator.RamBank.ToArray();
        var vram = emulator.Vera.Vram.ToArray();
        var frame = emulator.Vera.Frame_Count;

        RunHits(emulator, 200);
        Assert.AreNotEqual(clock, emulator.Clock);
        Assert.AreNotEqual(memory[0x1000], emulator.Memory[0x1000]);

        emulator.RestoreState(saved);

        Assert.AreEqual(clock, emulator.Clock);
        Assert.AreEqual(pc, emulator.Pc);
        Assert.AreEqual(a, emulator.A);
        Assert.AreEqual(x, emulator.X);
        Assert.AreEqual(y, emulator.Y);
        Assert.AreEqual(sp, emulator.StackPointer);
        Assert.AreEqual(frame, emulator.Vera.Frame_Count);
        CollectionAssert.AreEqual(memory, emulator.Memory.ToArray());
        CollectionAssert.AreEqual(ramBank, emulator.RamBank.ToArray());
        CollectionAssert.AreEqual(vram, emulator.Vera.Vram.ToArray());
    }

    [TestMethod]
    public async Task RestoreState_RunsTheSame()
    {
        using var emulator = Create();
        emulator.Breakpoints[Loop] = 1;

        await X16TestHelper.Emulate(Program, emulator, expectedResult: Emulator.EmulatorResult.Breakpoint);
        RunHits(emulator, 10);

        var saved = emulator.SaveState();

        Assert.AreEqual(Emulator.EmulatorResult.DebugOpCode, RunToEnd(emulator));
        var first = Capture(emulator);

        emulator.RestoreState(saved);

        Assert.AreEqual(Emulator.EmulatorResult.DebugOpCode, RunToEnd(emulator));
        var second = Capture(emulator);

        Assert.AreEqual(50, emulator.Memory[0x02]);
        Assert.AreEqual(first.Clock, second.Clock);
        Assert.AreEqual(first.Pc, second.Pc);
        Assert.AreEqual(first.Frame, second.Frame);
        Assert.AreEqual(first.Beam, second.Beam);
        CollectionAssert.AreEqual(first.Memory, second.Memory);
        CollectionAssert.AreEqual(first.Vram, second.Vram);
    }

    [TestMethod]
    public async Task RestoreState_KeepsBreakpoints()
    {
        using var emulator = Create();
        emulator.Breakpoints[Loop] = 1;

        await X16TestHelper.Emulate(Program, emulator, expectedResult: Emulator.EmulatorResult.Breakpoint);

        var saved = emulator.SaveState();

        // breakpoints changed after the save are what's used after the restore
        emulator.Breakpoints[Loop] = 0;
        emulator.SetBeamBreakpoints([Emulator.BeamOffset(0, 240)]);

        RunHits(emulator, 5);
        emulator.RestoreState(saved);

        Assert.AreEqual(0u, emulator.Breakpoints[Loop] & 1); // the core keeps its own flags in the other bits
        Assert.AreEqual(Emulator.BeamOffset(0, 240), emulator.GetBeamBreakpointOffset(0));
        Assert.IsTrue(emulator.Vera.Beam_Breakpoint_Next > emulator.Clock, "Beam breakpoint should be ahead of the restored clock.");

        Assert.AreEqual(Emulator.EmulatorResult.Breakpoint, emulator.EmulateWithTimeout());
        Assert.IsTrue((emulator.BreakpointSource & Emulator.BreakpointSourceType.Beam) != 0);
        Assert.AreEqual(240, emulator.Vera.Beam_Y);
    }

    [TestMethod]
    public void RestoreState_RejectsOtherHistorySize()
    {
        using var emulator = new Emulator();
        var saved = emulator.SaveState();

        emulator.SetOptions(new EmulatorOptions { HistorySize = emulator.Options.HistorySize * 2 });

        Assert.ThrowsException<ArgumentException>(() => emulator.RestoreState(saved));
    }

    private static void RunHits(Emulator emulator, int count)
    {
        for (var i = 0; i < count; i++)
            Assert.AreEqual(Emulator.EmulatorResult.Breakpoint, emulator.EmulateWithTimeout(), $"Hit {i}");
    }

    private static Emulator.EmulatorResult RunToEnd(Emulator emulator)
    {
        Emulator.EmulatorResult result;
        while ((result = emulator.EmulateWithTimeout()) == Emulator.EmulatorResult.Breakpoint)
        {
        }

        return result;
    }

    private static (ulong Clock, ushort Pc, uint Frame, uint Beam, byte[] Memory, byte[] Vram) Capture(Emulator emulator) =>
        (emulator.Clock, emulator.Pc, emulator.Vera.Frame_Count, emulator.Vera.Beam_Position, emulator.Memory.ToArray(), emulator.Vera.Vram.ToArray());

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
