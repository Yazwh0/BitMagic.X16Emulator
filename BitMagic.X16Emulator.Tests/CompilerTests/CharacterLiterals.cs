using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

// Spaces are removed from an opcode's parameters so the addressing mode templates match, but not
// from inside a character literal - `#' '` used to become `#''`, an empty literal.
[TestClass]
public class CharacterLiterals
{
    [TestMethod]
    public async Task Space()
    {
        var emulator = new Emulator();

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #' '
                stp",
                emulator);

        emulator.AssertState(A: 0x20);
    }

    [TestMethod]
    public async Task Space_Compare()
    {
        var emulator = new Emulator();

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #$20
                cmp #' '
                stp",
                emulator);

        emulator.AssertFlags(Zero: true, Carry: true);
    }

    [TestMethod]
    public async Task Letter_WithSpacesAroundIt()
    {
        var emulator = new Emulator();

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda # 'a' + 1
                stp",
                emulator);

        emulator.AssertState(A: 0x62);
    }
}
