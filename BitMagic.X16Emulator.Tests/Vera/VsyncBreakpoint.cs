using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

[TestClass]
public class VsyncBreakpoint
{
    // counts vsync irqs in $02
    private const string Program = @"
                .machine CommanderX16R40
                .org $810
                lda #01
                sta IEN
                cli
        .loop:
                inx
                jmp loop
                .org $900
                inc $02
                lda #01
                sta ISR
                rti";

    private static readonly HashSet<int> LoopPcs = [0x816, 0x817];

    [TestMethod]
    public async Task Stop_KeepsTheIrq()
    {
        using var emulator = new Emulator();

        emulator.InterruptHit = InterruptSource.None;
        emulator.InterruptMask = InterruptSource.None;
        emulator.RomBank[0x3ffe] = 0x00;
        emulator.RomBank[0x3fff] = 0x09;
        emulator.Vera.Frame_Count_Breakpoint = 1;

        await X16TestHelper.Emulate(Program, emulator, expectedResult: Emulator.EmulatorResult.Breakpoint);

        for (var i = 0; i < 8; i++)
        {
            if (i != 0)
            {
                emulator.Vera.Frame_Count_Breakpoint = emulator.Vera.Frame_Count + 1;
                Assert.AreEqual(Emulator.EmulatorResult.Breakpoint, emulator.EmulateWithTimeout(), $"Stop {i}");
            }

            var frame = emulator.Vera.Frame_Count;
            var message = $"Stop {i}, frame {frame}, pc ${emulator.Pc:X4}";

            Assert.IsTrue((emulator.BreakpointSource & Emulator.BreakpointSourceType.Vsync) != 0, message);
            Assert.IsTrue(LoopPcs.Contains(emulator.Pc), message); // stopped in the program, the irq hasn't been taken yet

            // every vsync before this one has had its irq, this one's is pending
            Assert.AreEqual((byte)(frame - 1), emulator.Memory[0x02], message);
            Assert.IsTrue((emulator.InterruptHit & InterruptSource.Vsync) != 0, $"{message}, vsync irq should be pending");
        }
    }
}
