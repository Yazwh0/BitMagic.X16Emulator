using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

[TestClass]
public class ROL
{
    [TestMethod]
    public async Task A()
    {
        using var emulator = new Emulator();

        emulator.A = 0b00000010;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                rol
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x2a, emulator.Memory[0x810]);

        // emulation
        emulator.AssertState(0b00000100, 0x00, 0x00, 0x812, 2);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task A_CarrySet()
    {
        using var emulator = new Emulator();

        emulator.A = 0b00000010;
        emulator.Carry = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                rol
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x2a, emulator.Memory[0x810]);

        // emulation
        emulator.AssertState(0b00000101, 0x00, 0x00, 0x812, 2);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task A_Zero()
    {
        using var emulator = new Emulator();

        emulator.A = 0b00000000;
        emulator.Carry = false;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                rol
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x2a, emulator.Memory[0x810]);

        // emulation
        emulator.AssertState(0b00000000, 0x00, 0x00, 0x812, 2);
        emulator.AssertFlags(true, false, false, false);
    }

    [TestMethod]
    public async Task A_NotZero()
    {
        using var emulator = new Emulator();

        emulator.A = 0b00000000;
        emulator.Carry = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                rol
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x2a, emulator.Memory[0x810]);

        // emulation
        emulator.AssertState(0b00000001, 0x00, 0x00, 0x812, 2);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task A_SetCarry()
    {
        using var emulator = new Emulator();

        emulator.A = 0b10000010;
        emulator.Carry = false;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                rol
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x2a, emulator.Memory[0x810]);

        // emulation
        emulator.AssertState(0b00000100, 0x00, 0x00, 0x812, 2);
        emulator.AssertFlags(false, false, false, true);
    }

    [TestMethod]
    public async Task Abs()
    {
        using var emulator = new Emulator();

        emulator.Memory[0x1234] = 0b00000010;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                rol $1234
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x2e, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(emulator.Memory[0x1234], 0b00000100);
        emulator.AssertState(0x00, 0x00, 0x00, 0x814, 6);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task Abs_CarrySet()
    {
        using var emulator = new Emulator();

        emulator.Memory[0x1234] = 0b00000010;
        emulator.Carry = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                rol $1234
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x2e, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(emulator.Memory[0x1234], 0b00000101);
        emulator.AssertState(0x00, 0x00, 0x00, 0x814, 6);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task Abs_SetCarry()
    {
        using var emulator = new Emulator();

        emulator.Memory[0x1234] = 0b10000010;
        emulator.Carry = false;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                rol $1234
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x2e, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(emulator.Memory[0x1234], 0b00000100);
        emulator.AssertState(0x00, 0x00, 0x00, 0x814, 6);
        emulator.AssertFlags(false, false, false, true);
    }

    [TestMethod]
    public async Task Abs_Zero()
    {
        using var emulator = new Emulator();

        emulator.Memory[0x1234] = 0b00000000;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                rol $1234
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x2e, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(emulator.Memory[0x1234], 0b00000000);
        emulator.AssertState(0x00, 0x00, 0x00, 0x814, 6);
        emulator.AssertFlags(true, false, false, false);
    }

    [TestMethod]
    public async Task AbsX()
    {
        using var emulator = new Emulator();

        emulator.Memory[0x1234] = 0b00000010;
        emulator.X = 0x34;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                rol $1200, x
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x3e, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(0b00000100, emulator.Memory[0x1234]);
        emulator.AssertState(0x00, 0x34, 0x00, 0x814, 7);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task AbsX_PageChange()
    {
        using var emulator = new Emulator();

        emulator.Memory[0x1310] = 0b00000010;
        emulator.X = 0x11;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                rol $12ff, x
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x3e, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(0b00000100, emulator.Memory[0x1310]);
        emulator.AssertState(0x00, 0x11, 0x00, 0x814, 7);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task AbsX_CarrySet()
    {
        using var emulator = new Emulator();

        emulator.Memory[0x1234] = 0b00000010;
        emulator.Carry = true;
        emulator.X = 0x34;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                rol $1200, x
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x3e, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(0b00000101, emulator.Memory[0x1234]);
        emulator.AssertState(0x00, 0x34, 0x00, 0x814, 7);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task AbsX_SetCarry()
    {
        using var emulator = new Emulator();

        emulator.Memory[0x1234] = 0b10000010;
        emulator.Carry = false;
        emulator.X = 0x34;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                rol $1200, x
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x3e, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(0b00000100, emulator.Memory[0x1234]);
        emulator.AssertState(0x00, 0x34, 0x00, 0x814, 7);
        emulator.AssertFlags(false, false, false, true);
    }

    [TestMethod]
    public async Task Zp()
    {
        using var emulator = new Emulator();

        emulator.Memory[0x12] = 0b00000010;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                rol $12
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x26, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(emulator.Memory[0x12], 0b00000100);
        emulator.AssertState(0x00, 0x00, 0x00, 0x813, 5);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task Zp_NotZero()
    {
        using var emulator = new Emulator();

        emulator.Memory[0x12] = 0b00000000;
        emulator.Carry = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                rol $12
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x26, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(emulator.Memory[0x12], 0b00000001);
        emulator.AssertState(0x00, 0x00, 0x00, 0x813, 5);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task Zp_CarrySet()
    {
        using var emulator = new Emulator();

        emulator.Memory[0x12] = 0b00000010;
        emulator.Carry = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                rol $12
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x26, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(emulator.Memory[0x12], 0b00000101);
        emulator.AssertState(0x00, 0x00, 0x00, 0x813, 5);
        emulator.AssertFlags(false, false, false, false);
    }


    [TestMethod]
    public async Task Zp_CarrySetZero()
    {
        using var emulator = new Emulator();

        emulator.Memory[0x12] = 0b00000010;
        emulator.Carry = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                rol $12
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x26, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(emulator.Memory[0x12], 0b00000101);
        emulator.AssertState(0x00, 0x00, 0x00, 0x813, 5);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task Zp_SetCarry()
    {
        using var emulator = new Emulator();

        emulator.Memory[0x12] = 0b10000010;
        emulator.Carry = false;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                rol $12
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x26, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(emulator.Memory[0x12], 0b00000100);
        emulator.AssertState(0x00, 0x00, 0x00, 0x813, 5);
        emulator.AssertFlags(false, false, false, true);
    }


    [TestMethod]
    public async Task ZpX()
    {
        using var emulator = new Emulator();

        emulator.Memory[0x12] = 0b00000010;
        emulator.X = 0x02;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                rol $10, x
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x36, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(emulator.Memory[0x12], 0b00000100);
        emulator.AssertState(0x00, 0x02, 0x00, 0x813, 6);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task ZpX_CarrySet()
    {
        using var emulator = new Emulator();

        emulator.Memory[0x12] = 0b00000010;
        emulator.Carry = true;
        emulator.X = 0x02;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                rol $10, x
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x36, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(emulator.Memory[0x12], 0b00000101);
        emulator.AssertState(0x00, 0x02, 0x00, 0x813, 6);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task ZpX_SetCarry()
    {
        using var emulator = new Emulator();

        emulator.Memory[0x12] = 0b10000010;
        emulator.Carry = false;
        emulator.X = 0x02;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                rol $10, x
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x36, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(emulator.Memory[0x12], 0b00000100);
        emulator.AssertState(0x00, 0x02, 0x00, 0x813, 6);
        emulator.AssertFlags(false, false, false, true);
    }

    [TestMethod]
    public async Task ZpX_Wrap()
    {
        using var emulator = new Emulator();

        emulator.Memory[0x12] = 0b10000010;
        emulator.Carry = false;
        emulator.X = 0x72;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                rol $a0, x
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x36, emulator.Memory[0x810]);

        // emulation
        Assert.AreEqual(emulator.Memory[0x12], 0b00000100);
        emulator.AssertState(0x00, 0x72, 0x00, 0x813, 6);
        emulator.AssertFlags(false, false, false, true);
    }

    [TestMethod]
    public async Task Readonly_Abs()
    {
        using var emulator = new Emulator();

        emulator.RomBank[0x0000] = 0b00000010;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                rol $c000
                stp",
                emulator);

        // emulation
        Assert.AreEqual(0b00000010, emulator.RomBank[0x0000]);

        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task Readonly_AbsSet()
    {
        using var emulator = new Emulator();

        emulator.RomBank[0x0000] = 0b10000001;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                rol $c000
                stp",
                emulator);

        // emulation
        Assert.AreEqual(0b10000001, emulator.RomBank[0x0000]);

        emulator.AssertFlags(false, false, false, true);
    }

    [TestMethod]
    public async Task Readonly_AbsX()
    {
        using var emulator = new Emulator();

        emulator.RomBank[0x0002] = 0b00000010;
        emulator.X = 0x02;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                rol $c000, x
                stp",
                emulator);

        // emulation
        Assert.AreEqual(0b00000010, emulator.RomBank[0x0002]);

        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task Readonly_AbsXSet()
    {
        using var emulator = new Emulator();

        emulator.RomBank[0x0002] = 0b10000001;
        emulator.X = 0x02;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                rol $c000, x
                stp",
                emulator);

        // emulation
        Assert.AreEqual(0b10000001, emulator.RomBank[0x0002]);

        emulator.AssertFlags(false, false, false, true);
    }
}