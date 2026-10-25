using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Via;

[TestClass]
public class Ifr
{
    [TestMethod]
    public async Task Ifr_Clear()
    {
        using var emulator = new Emulator();
        emulator.Memory[0x9f0d] = 0x01;
        emulator.A = 0x01;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta $9f0d
                stp",
                emulator);

        Assert.AreEqual(0x00, emulator.Memory[0x9f0d]);
    }

    [TestMethod]
    public async Task Ifr_ClearAll()
    {
        using var emulator = new Emulator();
        emulator.Memory[0x9f0d] = 0x7f;
        emulator.A = 0x7f;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta $9f0d
                stp",
                emulator);

        Assert.AreEqual(0x00, emulator.Memory[0x9f0d]);
    }

    [TestMethod]
    public async Task Ifr_ClearOne()
    {
        using var emulator = new Emulator();
        emulator.Memory[0x9f0d] = 0x7f;
        emulator.A = 0x01;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta $9f0d
                stp",
                emulator);

        Assert.AreEqual(0xfe, emulator.Memory[0x9f0d]);
    }

    [TestMethod]
    public async Task Ifr_HighBitIgnored()
    {
        using var emulator = new Emulator();
        emulator.Memory[0x9f0d] = 0x7f;
        emulator.A = 0x80;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta $9f0d
                stp",
                emulator);

        Assert.AreEqual(0xff, emulator.Memory[0x9f0d]);
    }

    [TestMethod]
    public async Task Ifr_HighBitNotClearable()
    {
        using var emulator = new Emulator();
        emulator.Memory[0x9f0d] = 0xff;
        emulator.A = 0x80;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta $9f0d
                stp",
                emulator);

        Assert.AreEqual(0xff, emulator.Memory[0x9f0d]);
    }
}