using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

[TestClass]
public class ADC_Decimal
{
    [TestMethod]
    public async Task Imm()
    {
        var emulator = new Emulator();

        emulator.A = 0x02;
        emulator.Decimal = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                adc #$03
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x69, emulator.Memory[0x810]);
        Assert.AreEqual(0x03, emulator.Memory[0x811]);

        // emulation
        emulator.AssertState(0x05, 0x00, 0x00, 0x813, 2);
        emulator.AssertFlags(false, false, false, false, false, true);
    }

    [TestMethod]
    public async Task Imm_ToZero()
    {
        var emulator = new Emulator();

        emulator.A = 0x97;
        emulator.Decimal = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                adc #$03
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x69, emulator.Memory[0x810]);
        Assert.AreEqual(0x03, emulator.Memory[0x811]);

        // emulation
        emulator.AssertState(0x00, 0x00, 0x00, 0x813, 2);
        emulator.AssertFlags(true, false, false, true, false, true);
    }

    [TestMethod]
    public async Task To_Overflow()
    {
        var emulator = new Emulator();

        emulator.A = 0x98;
        emulator.Decimal = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                adc #$03
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x69, emulator.Memory[0x810]);
        Assert.AreEqual(0x03, emulator.Memory[0x811]);

        // emulation
        emulator.AssertState(0x01, 0x00, 0x00, 0x813, 2);
        emulator.AssertFlags(false, false, false, true, false, true);
    }

}