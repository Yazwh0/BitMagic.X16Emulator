using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

[TestClass]
public class SBC_Decimal
{
    [TestMethod]
    public async Task Imm()
    {
        var emulator = new Emulator();

        emulator.A = 0x03;
        emulator.Carry = true;
        emulator.Decimal = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sbc #$02
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0xe9, emulator.Memory[0x810]);
        Assert.AreEqual(0x02, emulator.Memory[0x811]);

        // emulation
        emulator.AssertState(0x01, 0x00, 0x00, 0x813, 2);
        emulator.AssertFlags(false, false, false, true, false, true);
    }

    [TestMethod]
    public async Task Imm_ToZero()
    {
        var emulator = new Emulator();

        emulator.A = 0x03;
        emulator.Carry = true;
        emulator.Decimal = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sbc #$03
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0xe9, emulator.Memory[0x810]);
        Assert.AreEqual(0x03, emulator.Memory[0x811]);

        // emulation
        emulator.AssertState(0x00, 0x00, 0x00, 0x813, 2);
        emulator.AssertFlags(true, false, false, true, false, true);
    }

    [TestMethod]
    public async Task Imm_Overflow()
    {
        var emulator = new Emulator();

        emulator.A = 0x03;
        emulator.Carry = true;
        emulator.Decimal = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sbc #$04
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0xe9, emulator.Memory[0x810]);
        Assert.AreEqual(0x04, emulator.Memory[0x811]);

        // emulation
        emulator.AssertState(0x99, 0x00, 0x00, 0x813, 2);
        emulator.AssertFlags(false, true, false, false, false, true);
    }
}