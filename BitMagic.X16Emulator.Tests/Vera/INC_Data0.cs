using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

[TestClass]
public class INC_Data0
{
    [TestMethod]
    public async Task Abs_Step0()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Step = 0;
        emulator.Vera.Data0_Address = 0x0000;
        emulator.Vera.Vram[0x0000] = 0x20;
        emulator.Vera.Vram[0x0001] = 0x30;
        emulator.Vera.Vram[0x0002] = 0x40;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                inc DATA0
                stp",
                emulator);

        Assert.AreEqual(0x00000, emulator.Vera.Data0_Address);
        Assert.AreEqual(0x21, emulator.Memory[0x9F23]);

        Assert.AreEqual(0x00, emulator.Memory[0x9F20]);
        Assert.AreEqual(0x00, emulator.Memory[0x9F21]);
        Assert.AreEqual(0x00, emulator.Memory[0x9F22]);
    }

    [TestMethod]
    public async Task Abs_Step1()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Step = 1;
        emulator.Vera.Data0_Address = 0x0000;
        emulator.Vera.Vram[0x0000] = 0x20;
        emulator.Vera.Vram[0x0001] = 0x30;
        emulator.Vera.Vram[0x0002] = 0x40;
        emulator.Vera.Vram[0x0003] = 0x50;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                inc DATA0
                stp",
                emulator);

        Assert.AreEqual(0x00003, emulator.Vera.Data0_Address);
        Assert.AreEqual(0x20, emulator.Vera.Vram[0x0000]);
        Assert.AreEqual(0x30, emulator.Vera.Vram[0x0001]);
        Assert.AreEqual(0x21, emulator.Vera.Vram[0x0002]);

        Assert.AreEqual(0x50, emulator.Memory[0x9F23]);

        Assert.AreEqual(0x03, emulator.Memory[0x9F20]);
        Assert.AreEqual(0x00, emulator.Memory[0x9F21]);
        Assert.AreEqual(0x10, emulator.Memory[0x9F22]);
    }

    [TestMethod]
    public async Task AbsX_Step0()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Step = 0;
        emulator.Vera.Data0_Address = 0x0000;
        emulator.Vera.Vram[0x0000] = 0x20;
        emulator.Vera.Vram[0x0001] = 0x30;
        emulator.Vera.Vram[0x0002] = 0x40;
        emulator.X = 0x23;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                inc $9f00, x
                stp",
                emulator);

        Assert.AreEqual(0x00000, emulator.Vera.Data0_Address);
        Assert.AreEqual(0x21, emulator.Memory[0x9F23]);

        Assert.AreEqual(0x00, emulator.Memory[0x9F20]);
        Assert.AreEqual(0x00, emulator.Memory[0x9F21]);
        Assert.AreEqual(0x00, emulator.Memory[0x9F22]);
    }

    [TestMethod]
    public async Task AbsX_Step1()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Step = 1;
        emulator.Vera.Data0_Address = 0x0000;
        emulator.Vera.Vram[0x0000] = 0x20;
        emulator.Vera.Vram[0x0001] = 0x30;
        emulator.Vera.Vram[0x0002] = 0x40;
        emulator.Vera.Vram[0x0003] = 0x50;
        emulator.Vera.Vram[0x0004] = 0x60;
        emulator.X = 0x23;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                inc $9f00, x
                stp",
                emulator);

        Assert.AreEqual(0x00004, emulator.Vera.Data0_Address);
        Assert.AreEqual(0x20, emulator.Vera.Vram[0x0000]);
        Assert.AreEqual(0x30, emulator.Vera.Vram[0x0001]);
        Assert.AreEqual(0x40, emulator.Vera.Vram[0x0002]);
        Assert.AreEqual(0x21, emulator.Vera.Vram[0x0003]);

        Assert.AreEqual(0x60, emulator.Memory[0x9F23]);

        Assert.AreEqual(0x04, emulator.Memory[0x9F20]);
        Assert.AreEqual(0x00, emulator.Memory[0x9F21]);
        Assert.AreEqual(0x10, emulator.Memory[0x9F22]);
    }

}