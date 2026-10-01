using BitMagic.Compiler.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

[TestClass]
public class BranchRange
{
    private static string Nops(int count) => string.Join("\n", Enumerable.Repeat("nop", count));

    private static async Task<CompilerBranchToFarException> ExpectTooFar(string code)
    {
        var emulator = new Emulator();

        try
        {
            await X16TestHelper.Emulate(code, emulator);
        }
        catch (CompilerBranchToFarException e)
        {
            return e;
        }

        Assert.Fail("No exception hit.");
        return null!;
    }

    [TestMethod]
    public async Task Forward_MaxOffset_Compiles()
    {
        var emulator = new Emulator();

        await X16TestHelper.Emulate($@"
            .machine CommanderX16R40
            .org $810
            bne target
            {Nops(127)}
        .target:
            stp", emulator);

        Assert.AreEqual(0xd0, emulator.Memory[0x810]);
        Assert.AreEqual(0x7f, emulator.Memory[0x811]);
    }

    [TestMethod]
    public async Task Backward_MaxOffset_Compiles()
    {
        var emulator = new Emulator();

        await X16TestHelper.Emulate($@"
            .machine CommanderX16R40
            .org $810
            lda #$00
        .start:
            {Nops(126)}
            bne start
            stp", emulator);

        Assert.AreEqual(0xd0, emulator.Memory[0x810 + 2 + 126]);
        Assert.AreEqual(0x80, emulator.Memory[0x810 + 2 + 126 + 1]);
    }

    [TestMethod]
    public async Task Forward_OneTooFar_ReportsLabelAndAmount()
    {
        var e = await ExpectTooFar($@"
            .machine CommanderX16R40
            .org $810
            beq target
            {Nops(128)}
        .target:
            stp");

        StringAssert.Contains(e.Message, "'target'");
        StringAssert.Contains(e.Message, "out of range by 1 byte ");
    }

    [TestMethod]
    public async Task Forward_Far_ReportsAmount()
    {
        var e = await ExpectTooFar($@"
            .machine CommanderX16R40
            .org $810
            bne target
            {Nops(200)}
        .target:
            stp");

        StringAssert.Contains(e.Message, "out of range by 73 bytes");
    }

    [TestMethod]
    public async Task Backward_OneTooFar_ReportsLabelAndAmount()
    {
        var e = await ExpectTooFar($@"
            .machine CommanderX16R40
            .org $810
            lda #$00
        .start:
            {Nops(127)}
            bne start
            stp");

        StringAssert.Contains(e.Message, "'start'");
        StringAssert.Contains(e.Message, "out of range by 1 byte ");
    }

    [TestMethod]
    public async Task Exception_PointsAtBranchLine()
    {
        var e = await ExpectTooFar($@"
            .machine CommanderX16R40
            .org $810
            nop
            bcc target
            {Nops(150)}
        .target:
            stp");

        Assert.AreEqual(0x811, e.Line.Address);
    }
}
