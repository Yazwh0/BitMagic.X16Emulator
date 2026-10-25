using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

[TestClass]
public class DEX
{
    [TestMethod]
    public async Task Dex()
    {
        using var emulator = new Emulator();

        emulator.X = 2;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                dex
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0xca, emulator.Memory[0x810]);

        // emulation
        emulator.AssertState(0x00, 0x01, 0x00, 0x812, 2);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task Dex_ToZero()
    {
        using var emulator = new Emulator();

        emulator.X = 1;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                dex
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0xca, emulator.Memory[0x810]);

        // emulation
        emulator.AssertState(0x00, 0x00, 0x00, 0x812, 2);
        emulator.AssertFlags(true, false, false, false);
    }

    [TestMethod]
    public async Task Dex_Negative()
    {
        using var emulator = new Emulator();

        emulator.X = 0xa0;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                dex
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0xca, emulator.Memory[0x810]);

        // emulation
        emulator.AssertState(0x00, 0x9f, 0x00, 0x812, 2);
        emulator.AssertFlags(false, true, false, false);
    }

    [TestMethod]
    public async Task Dex_ToNegative()
    {
        using var emulator = new Emulator();

        emulator.X = 0;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                dex
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0xca, emulator.Memory[0x810]);

        // emulation
        emulator.AssertState(0x00, 0xff, 0x00, 0x812, 2);
        emulator.AssertFlags(false, true, false, false);
    }
}
