using BitMagic.Compiler.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

[TestClass]
public class AnonymousLabels
{
    // `.: dex` puts the anonymous label on dex's own address, then `.: bne -` puts a second
    // anonymous label on the bne's own address before resolving "-". Before the fix, "-"
    // matched that second (self) label, branching the bne to itself and looping forever.
    [TestMethod]
    public async Task SharedOpcode_Backward_DoesNotReferenceSelf()
    {
        var emulator = new Emulator();

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ldx #$03
            .: dex
            .: bne -
                stp",
                emulator);

        // compilation: "-" must resolve to the dex label at $812, not to bne's own address ($813)
        Assert.AreEqual(0xd0, emulator.Memory[0x813]);
        Assert.AreEqual(0xfd, emulator.Memory[0x814]); // offset back to $812

        // emulation: the loop runs to completion. It would never reach stp if "-" branched to itself.
        emulator.AssertState(0x00, 0x00, 0x00, 0x816, 16); // ldx(2) + 2x[dex(2)+bne taken(3)] + dex(2)+bne not taken(2)
        emulator.AssertFlags(true, false, false, false);
    }

    // Same bug, forward direction: `.: bne +` puts the anonymous label on bne's own address
    // before resolving "+". Before the fix, "+" matched that same label instead of the next one.
    [TestMethod]
    public async Task SharedOpcode_Forward_DoesNotReferenceSelf()
    {
        var emulator = new Emulator();
        emulator.Zero = false; // ensure the branch is taken

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
            .: bne +
                stp
            .: nop
                stp",
                emulator);

        // compilation: "+" must resolve to the nop label at $813, not to bne's own address ($810)
        Assert.AreEqual(0xd0, emulator.Memory[0x810]);
        Assert.AreEqual(0x01, emulator.Memory[0x811]); // offset forward to $813

        // emulation: the branch lands on nop, not back on itself.
        emulator.AssertState(Pc: 0x815, Clock: 5); // bne taken(3) + nop(2)
        emulator.AssertFlags(false, false, false, false);
    }

    // With no other anonymous label to find in that direction, the self-reference must now be
    // a compile error rather than silently resolving to the opcode's own address.
    [TestMethod]
    public async Task SharedOpcode_Backward_NoOtherLabel_ThrowsOnCompile()
    {
        await Assert.ThrowsExceptionAsync<RelativeLabelException>(() =>
            X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
            .: bne -
                stp"));
    }

    [TestMethod]
    public async Task SharedOpcode_Forward_NoOtherLabel_ThrowsOnCompile()
    {
        await Assert.ThrowsExceptionAsync<RelativeLabelException>(() =>
            X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
            .: bne +
                stp"));
    }
}
