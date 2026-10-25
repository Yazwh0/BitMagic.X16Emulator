using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

[TestClass]
public class ORA
{
    [TestMethod]
    public async Task Imm()
    {
        using var emulator = new Emulator();

        emulator.A = 0x2;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora #$03
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x09, emulator.Memory[0x810]);
        Assert.AreEqual(0x03, emulator.Memory[0x811]);

        // emulation
        emulator.AssertState(0x03, 0x00, 0x00, 0x813, 2);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task Imm_Zero()
    {
        using var emulator = new Emulator();

        emulator.A = 0x00;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora #$00
                stp",
                emulator);

        // emulation
        emulator.AssertState(0x00, 0x00, 0x00, 0x813, 2);
        emulator.AssertFlags(true, false, false, false);
    }

    [TestMethod]
    public async Task Imm_Negative()
    {
        using var emulator = new Emulator();

        emulator.A = 0b00000001;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora #0b10000000
                stp",
                emulator);

        // emulation
        emulator.AssertState(0b10000001, 0x00, 0x00, 0x813, 2);
        emulator.AssertFlags(false, true, false, false);
    }

    [TestMethod]
    public async Task Imm_CarryPreserve()
    {
        using var emulator = new Emulator();

        emulator.A = 0b10000000;
        emulator.Carry = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora #0b10000001
                stp",
                emulator);

        // emulation
        emulator.AssertState(0b10000001, 0x00, 0x00, 0x813, 2);
        emulator.AssertFlags(false, true, false, true);
    }

    [TestMethod]
    public async Task Abs()
    {
        using var emulator = new Emulator();

        emulator.A = 0x2;
        emulator.Memory[0x1234] = 0x03;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora $1234
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x0d, emulator.Memory[0x810]);
        Assert.AreEqual(0x34, emulator.Memory[0x811]);
        Assert.AreEqual(0x12, emulator.Memory[0x812]);

        // emulation
        emulator.AssertState(0x03, 0x00, 0x00, 0x814, 4);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task Abs_Zero()
    {
        using var emulator = new Emulator();

        emulator.A = 0x00;
        emulator.Memory[0x1234] = 0x00;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora $1234
                stp",
                emulator);

        // emulation
        emulator.AssertState(0x00, 0x00, 0x00, 0x814, 4);
        emulator.AssertFlags(true, false, false, false);
    }

    [TestMethod]
    public async Task Abs_Negative()
    {
        using var emulator = new Emulator();

        emulator.A = 0b10000000;
        emulator.Memory[0x1234] = 0b00000001;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora $1234
                stp",
                emulator);

        // emulation
        emulator.AssertState(0b10000001, 0x00, 0x00, 0x814, 4);
        emulator.AssertFlags(false, true, false, false);
    }


    [TestMethod]
    public async Task Abs_CarryPreserve()
    {
        using var emulator = new Emulator();

        emulator.A = 0b10000000;
        emulator.Carry = true;
        emulator.Memory[0x1234] = 0b00000000;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora $1234
                stp",
                emulator);

        // emulation
        emulator.AssertState(0b10000000, 0x00, 0x00, 0x814, 4);
        emulator.AssertFlags(false, true, false, true);
    }

    [TestMethod]
    public async Task AbsX()
    {
        using var emulator = new Emulator();

        emulator.A = 0x2;
        emulator.Memory[0x1234] = 0x03;
        emulator.X = 0x34;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora $1200,x
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x1d, emulator.Memory[0x810]);
        Assert.AreEqual(0x00, emulator.Memory[0x811]);
        Assert.AreEqual(0x12, emulator.Memory[0x812]);

        // emulation
        emulator.AssertState(0x03, 0x34, 0x00, 0x814, 4);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task AbsX_Zero()
    {
        using var emulator = new Emulator();

        emulator.A = 0x00;
        emulator.Memory[0x1234] = 0x00;
        emulator.X = 0x34;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora $1200,x
                stp",
                emulator);

        // emulation
        emulator.AssertState(0x00, 0x34, 0x00, 0x814, 4);
        emulator.AssertFlags(true, false, false, false);
    }

    [TestMethod]
    public async Task AbsX_Negative()
    {
        using var emulator = new Emulator();

        emulator.A = 0b10000000;
        emulator.Memory[0x1234] = 0b00000001;
        emulator.X = 0x34;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora $1200,x
                stp",
                emulator);

        // emulation
        emulator.AssertState(0b10000001, 0x34, 0x00, 0x814, 4);
        emulator.AssertFlags(false, true, false, false);
    }


    [TestMethod]
    public async Task AbsX_CarryPreserve()
    {
        using var emulator = new Emulator();

        emulator.A = 0b10000001;
        emulator.Carry = true;
        emulator.Memory[0x1234] = 0b00000001;
        emulator.X = 0x34;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora $1200,x
                stp",
                emulator);

        // emulation
        emulator.AssertState(0b10000001, 0x34, 0x00, 0x814, 4);
        emulator.AssertFlags(false, true, false, true);
    }

    [TestMethod]
    public async Task AbsX_PagePenatly()
    {
        using var emulator = new Emulator();

        emulator.A = 0x2;
        emulator.Memory[0x1310] = 0x03;
        emulator.X = 0x70;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora $12a0,x
                stp",
                emulator);

        // emulation
        emulator.AssertState(0x03, 0x70, 0x00, 0x814, 5);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task AbsY()
    {
        using var emulator = new Emulator();

        emulator.A = 0x2;
        emulator.Memory[0x1234] = 0x03;
        emulator.Y = 0x34;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora $1200,y
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x19, emulator.Memory[0x810]);
        Assert.AreEqual(0x00, emulator.Memory[0x811]);
        Assert.AreEqual(0x12, emulator.Memory[0x812]);

        // emulation
        emulator.AssertState(0x03, 0x00, 0x34, 0x814, 4);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task AbsY_Zero()
    {
        using var emulator = new Emulator();

        emulator.A = 0x00;
        emulator.Memory[0x1234] = 0x00;
        emulator.Y = 0x34;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora $1200,y
                stp",
                emulator);

        // emulation
        emulator.AssertState(0x00, 0x00, 0x34, 0x814, 4);
        emulator.AssertFlags(true, false, false, false);
    }

    [TestMethod]
    public async Task AbsY_Negative()
    {
        using var emulator = new Emulator();

        emulator.A = 0b10000010;
        emulator.Memory[0x1234] = 0b00000010;
        emulator.Y = 0x34;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora $1200,y
                stp",
                emulator);

        // emulation
        emulator.AssertState(0b10000010, 0x00, 0x34, 0x814, 4);
        emulator.AssertFlags(false, true, false, false);
    }

    [TestMethod]
    public async Task AbsY_CarryPreserve()
    {
        using var emulator = new Emulator();

        emulator.A = 0b10000001;
        emulator.Carry = true;
        emulator.Memory[0x1234] = 0b00000001;
        emulator.Y = 0x34;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora $1200,y
                stp",
                emulator);

        // emulation
        emulator.AssertState(0b10000001, 0x00, 0x34, 0x814, 4);
        emulator.AssertFlags(false, true, false, true);
    }

    [TestMethod]
    public async Task AbsY_PagePenatly()
    {
        using var emulator = new Emulator();

        emulator.A = 0x2;
        emulator.Memory[0x1310] = 0x03;
        emulator.Y = 0x70;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora $12a0,y
                stp",
                emulator);

        // emulation
        emulator.AssertState(0x03, 0x00, 0x70, 0x814, 5);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task Zp()
    {
        using var emulator = new Emulator();

        emulator.A = 0x2;
        emulator.Memory[0x12] = 0x03;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora $12
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x05, emulator.Memory[0x810]);
        Assert.AreEqual(0x12, emulator.Memory[0x811]);

        // emulation
        emulator.AssertState(0x03, 0x00, 0x00, 0x813, 3);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task Zp_Zero()
    {
        using var emulator = new Emulator();

        emulator.A = 0x00;
        emulator.Memory[0x12] = 0x00;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora $12
                stp",
                emulator);

        // emulation
        emulator.AssertState(0x00, 0x00, 0x00, 0x813, 3);
        emulator.AssertFlags(true, false, false, false);
    }

    [TestMethod]
    public async Task Zp_Negative()
    {
        using var emulator = new Emulator();

        emulator.A = 0b10000001;
        emulator.Memory[0x12] = 0b00000001;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora $12
                stp",
                emulator);

        // emulation
        emulator.AssertState(0b10000001, 0x00, 0x00, 0x813, 3);
        emulator.AssertFlags(false, true, false, false);
    }

    [TestMethod]
    public async Task Zp_CarryPreserve()
    {
        using var emulator = new Emulator();

        emulator.A = 0b10000001;
        emulator.Carry = true;
        emulator.Memory[0x12] = 0b00000001;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora $12
                stp",
                emulator);

        // emulation
        emulator.AssertState(0b10000001, 0x00, 0x00, 0x813, 3);
        emulator.AssertFlags(false, true, false, true);
    }

    [TestMethod]
    public async Task ZpX()
    {
        using var emulator = new Emulator();

        emulator.A = 0x2;
        emulator.Memory[0x12] = 0x03;
        emulator.X = 0x02;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora $10,x
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x15, emulator.Memory[0x810]);
        Assert.AreEqual(0x10, emulator.Memory[0x811]);

        // emulation
        emulator.AssertState(0x03, 0x02, 0x00, 0x813, 4);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task ZpX_Zero()
    {
        using var emulator = new Emulator();

        emulator.A = 0x00;
        emulator.Memory[0x12] = 0x00;
        emulator.X = 0x02;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora $10,x
                stp",
                emulator);

        // emulation
        emulator.AssertState(0x00, 0x02, 0x00, 0x813, 4);
        emulator.AssertFlags(true, false, false, false);
    }

    [TestMethod]
    public async Task ZpX_Negative()
    {
        using var emulator = new Emulator();

        emulator.A = 0b10000001;
        emulator.Memory[0x12] = 0b00000001;
        emulator.X = 0x02;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora $10,x
                stp",
                emulator);

        // emulation
        emulator.AssertState(0b10000001, 0x02, 0x00, 0x813, 4);
        emulator.AssertFlags(false, true, false, false);
    }

    [TestMethod]
    public async Task ZpX_CarryPreserve()
    {
        using var emulator = new Emulator();

        emulator.A = 0b10000001;
        emulator.Carry = true;
        emulator.Memory[0x12] = 0b00000001;
        emulator.X = 0x02;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora $10,x
                stp",
                emulator);

        // emulation
        emulator.AssertState(0b10000001, 0x02, 0x00, 0x813, 4);
        emulator.AssertFlags(false, true, false, true);
    }

    [TestMethod]
    public async Task IndZp()
    {
        using var emulator = new Emulator();

        emulator.A = 0x2;
        emulator.Memory[0x1234] = 0x03;
        emulator.Memory[0x12] = 0x34;
        emulator.Memory[0x13] = 0x12;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora ($12)
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x12, emulator.Memory[0x810]);
        Assert.AreEqual(0x12, emulator.Memory[0x811]);

        // emulation
        emulator.AssertState(0x03, 0x00, 0x00, 0x813, 5);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task IndZp_Zero()
    {
        using var emulator = new Emulator();

        emulator.A = 0x00;
        emulator.Memory[0x1234] = 0x00;
        emulator.Memory[0x12] = 0x34;
        emulator.Memory[0x13] = 0x12;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora ($12)
                stp",
                emulator);

        // emulation
        emulator.AssertState(0x00, 0x00, 0x00, 0x813, 5);
        emulator.AssertFlags(true, false, false, false);
    }

    [TestMethod]
    public async Task IndZp_Negative()
    {
        using var emulator = new Emulator();

        emulator.A = 0b10000001;
        emulator.Memory[0x1234] = 0b00000001;
        emulator.Memory[0x12] = 0x34;
        emulator.Memory[0x13] = 0x12;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora ($12)
                stp",
                emulator);

        // emulation
        emulator.AssertState(0b10000001, 0x00, 0x00, 0x813, 5);
        emulator.AssertFlags(false, true, false, false);
    }

    [TestMethod]
    public async Task IndZp_CarryPreserve()
    {
        using var emulator = new Emulator();

        emulator.A = 0b10000001;
        emulator.Carry = true;
        emulator.Memory[0x1234] = 0b00000001;
        emulator.Memory[0x12] = 0x34;
        emulator.Memory[0x13] = 0x12;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora ($12)
                stp",
                emulator);

        // emulation
        emulator.AssertState(0b10000001, 0x00, 0x00, 0x813, 5);
        emulator.AssertFlags(false, true, false, true);
    }

    [TestMethod]
    public async Task IndX()
    {
        using var emulator = new Emulator();

        emulator.A = 0x2;
        emulator.Memory[0x1234] = 0x03;
        emulator.X = 0x02;
        emulator.Memory[0x12] = 0x34;
        emulator.Memory[0x13] = 0x12;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora ($10,x)
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x01, emulator.Memory[0x810]);
        Assert.AreEqual(0x10, emulator.Memory[0x811]);

        // emulation
        emulator.AssertState(0x03, 0x02, 0x00, 0x813, 6);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task IndX_Zero()
    {
        using var emulator = new Emulator();

        emulator.A = 0x00;
        emulator.Memory[0x1234] = 0x00;
        emulator.X = 0x02;
        emulator.Memory[0x12] = 0x34;
        emulator.Memory[0x13] = 0x12;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora ($10,x)
                stp",
                emulator);

        // emulation
        emulator.AssertState(0x00, 0x02, 0x00, 0x813, 6);
        emulator.AssertFlags(true, false, false, false);
    }

    [TestMethod]
    public async Task IndX_Negative()
    {
        using var emulator = new Emulator();

        emulator.A = 0b10000001;
        emulator.Memory[0x1234] = 0b00000001;
        emulator.X = 0x02;
        emulator.Memory[0x12] = 0x34;
        emulator.Memory[0x13] = 0x12;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora ($10,x)
                stp",
                emulator);

        // emulation
        emulator.AssertState(0b10000001, 0x02, 0x00, 0x813, 6);
        emulator.AssertFlags(false, true, false, false);
    }

    [TestMethod]
    public async Task IndX_CarryPreserve()
    {
        using var emulator = new Emulator();

        emulator.A = 0b10000001;
        emulator.Carry = true;
        emulator.Memory[0x1234] = 0b00000001;
        emulator.X = 0x02;
        emulator.Memory[0x12] = 0x34;
        emulator.Memory[0x13] = 0x12;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora ($10,x)
                stp",
                emulator);

        // emulation
        emulator.AssertState(0b10000001, 0x02, 0x00, 0x813, 6);
        emulator.AssertFlags(false, true, false, true);
    }

    [TestMethod]
    public async Task IndY()
    {
        using var emulator = new Emulator();

        emulator.A = 0x2;
        emulator.Memory[0x1234] = 0x03;
        emulator.Y = 0x34;
        emulator.Memory[0x10] = 0x00;
        emulator.Memory[0x11] = 0x12;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora ($10),y
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0x11, emulator.Memory[0x810]);
        Assert.AreEqual(0x10, emulator.Memory[0x811]);

        // emulation
        emulator.AssertState(0x03, 0x00, 0x34, 0x813, 5);
        emulator.AssertFlags(false, false, false, false);
    }

    [TestMethod]
    public async Task IndY_Zero()
    {
        using var emulator = new Emulator();

        emulator.A = 0x00;
        emulator.Memory[0x1234] = 0x00;
        emulator.Y = 0x34;
        emulator.Memory[0x10] = 0x00;
        emulator.Memory[0x11] = 0x12;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora ($10),y
                stp",
                emulator);

        // emulation
        emulator.AssertState(0x00, 0x00, 0x34, 0x813, 5);
        emulator.AssertFlags(true, false, false, false);
    }

    [TestMethod]
    public async Task IndY_Negative()
    {
        using var emulator = new Emulator();

        emulator.A = 0b10000001;
        emulator.Memory[0x1234] = 0b00000001;
        emulator.Y = 0x34;
        emulator.Memory[0x10] = 0x00;
        emulator.Memory[0x11] = 0x12;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora ($10),y
                stp",
                emulator);

        // emulation
        emulator.AssertState(0b10000001, 0x00, 0x34, 0x813, 5);
        emulator.AssertFlags(false, true, false, false);
    }

    [TestMethod]
    public async Task IndY_CarryPreserve()
    {
        using var emulator = new Emulator();

        emulator.A = 0b10000001;
        emulator.Carry = true;
        emulator.Memory[0x1234] = 0b00000001;
        emulator.Y = 0x34;
        emulator.Memory[0x10] = 0x00;
        emulator.Memory[0x11] = 0x12;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora ($10),y
                stp",
                emulator);

        // emulation
        emulator.AssertState(0b10000001, 0x00, 0x34, 0x813, 5);
        emulator.AssertFlags(false, true, false, true);
    }

    [TestMethod]
    public async Task IndY_PagePenatly()
    {
        using var emulator = new Emulator();

        emulator.A = 0x2;
        emulator.Memory[0x1310] = 0x03;
        emulator.Y = 0x70;
        emulator.Memory[0x10] = 0xa0;
        emulator.Memory[0x11] = 0x12;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                ora ($10),y
                stp",
                emulator);

        // emulation
        emulator.AssertState(0x03, 0x00, 0x70, 0x813, 6);
        emulator.AssertFlags(false, false, false, false);
    }
}