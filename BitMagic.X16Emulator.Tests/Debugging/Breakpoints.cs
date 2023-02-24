using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

[TestClass]
public class Breakpoints
{
    [TestMethod]
    public async Task NormalRam()
    {
        var emulator = new Emulator();

        emulator.Breakpoints[0x811] = 1;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                nop
                nop ; breakpoint
                nop 
                stp",
                emulator, expectedResult: Emulator.EmulatorResult.Breakpoint);

        emulator.AssertState(Pc: 0x811);
    }
}
