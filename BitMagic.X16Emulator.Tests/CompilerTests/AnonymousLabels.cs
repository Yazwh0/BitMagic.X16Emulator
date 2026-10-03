using BitMagic.Compiler.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

[TestClass]
public class AnonymousLabels
{
    // `.: bne -` puts an anonymous label on the bne's own address before resolving "-". Backwards,
    // that label is the loop the bne is in, so "-" matches it rather than the earlier nop label.
    [TestMethod]
    public async Task SharedOpcode_Backward_ReferencesSelf()
    {
        var emulator = new Emulator();
        emulator.Zero = true; // the branch isn't taken, otherwise it would loop forever

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
            .: nop
            .: bne -
                stp",
                emulator);

        // compilation: "-" resolves to bne's own address ($811), not the nop label at $810
        Assert.AreEqual(0xd0, emulator.Memory[0x811]);
        Assert.AreEqual(0xfe, emulator.Memory[0x812]); // offset back to $811

        emulator.AssertState(Pc: 0x814, Clock: 4); // nop(2) + bne not taken(2)
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

    // With no other anonymous label, the label is unique so is resolved explicitly rather than as an
    // ambiguous label; backwards it still matches its own line, giving a jump to itself.
    [TestMethod]
    public async Task SharedOpcode_Backward_NoOtherLabel_ReferencesSelf()
    {
        var emulator = new Emulator();

        // the stp stops the emulation before the jmp, which would otherwise loop forever
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp
            .: jmp -",
                emulator);

        Assert.AreEqual(0x4c, emulator.Memory[0x811]); // jmp $0811
        Assert.AreEqual(0x11, emulator.Memory[0x812]);
        Assert.AreEqual(0x08, emulator.Memory[0x813]);
    }

    // Forwards a label never matches its own line, so with no later label it is a compile error.
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
