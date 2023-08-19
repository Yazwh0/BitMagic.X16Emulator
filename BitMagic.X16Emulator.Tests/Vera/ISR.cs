using BitMagic.Compiler;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Vera;

[TestClass]
public class Isr
{
    private const int AFLOW = 0x08;

    [TestMethod]
    public async Task Clear_Vsync()
    {
        var emulator = new Emulator();

        emulator.Vera.Interrupt_Vsync_Hit = true;
        emulator.Memory[0x9F27] = 0x01;     // hit not yet set in init.

        emulator.A = 0x01;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta ISR
                stp",
                emulator);

        Assert.AreEqual(AFLOW, emulator.Memory[0x9F27]);
        Assert.IsFalse(emulator.Vera.Interrupt_Vsync_Hit);
        Assert.IsFalse(emulator.Vera.Interrupt_Line_Hit);
        Assert.IsFalse(emulator.Vera.Interrupt_SpCol_Hit);
    }

    [TestMethod]
    public async Task Clear_Vsync_Preserve()
    {
        var emulator = new Emulator();

        emulator.Vera.Interrupt_Vsync_Hit = true;
        emulator.Vera.Interrupt_Line_Hit = true;
        emulator.Vera.Interrupt_SpCol_Hit = true;
        emulator.Memory[0x9F27] = 0xff;     // hit not yet set in init.

        emulator.A = 0x01;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta ISR
                stp",
                emulator);

        Assert.AreEqual(0xfe, emulator.Memory[0x9F27]);
        Assert.IsFalse(emulator.Vera.Interrupt_Vsync_Hit);
        Assert.IsTrue(emulator.Vera.Interrupt_Line_Hit);
        Assert.IsTrue(emulator.Vera.Interrupt_SpCol_Hit);
    }

    [TestMethod]
    public async Task Clear_Line()
    {
        var emulator = new Emulator();

        emulator.Vera.Interrupt_Line_Hit = true;
        emulator.Memory[0x9F27] = 0x02;     // hit not yet set in init.

        emulator.A = 0x02;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta ISR
                stp",
                emulator);

        Assert.AreEqual(AFLOW, emulator.Memory[0x9F27]);
        Assert.IsFalse(emulator.Vera.Interrupt_Vsync_Hit);
        Assert.IsFalse(emulator.Vera.Interrupt_Line_Hit);
        Assert.IsFalse(emulator.Vera.Interrupt_SpCol_Hit);
    }

    [TestMethod]
    public async Task Clear_Line_Preserve()
    {
        var emulator = new Emulator();

        emulator.Vera.Interrupt_Vsync_Hit = true;
        emulator.Vera.Interrupt_Line_Hit = true;
        emulator.Vera.Interrupt_SpCol_Hit = true;
        emulator.Memory[0x9F27] = 0xff;     // hit not yet set in init.

        emulator.A = 0x02;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta ISR
                stp",
                emulator);

        Assert.AreEqual(0xfd, emulator.Memory[0x9F27]);
        Assert.IsTrue(emulator.Vera.Interrupt_Vsync_Hit);
        Assert.IsFalse(emulator.Vera.Interrupt_Line_Hit);
        Assert.IsTrue(emulator.Vera.Interrupt_SpCol_Hit);
    }


    [TestMethod]
    public async Task Clear_Spcol()
    {
        var emulator = new Emulator();

        emulator.Vera.Interrupt_SpCol_Hit = true;
        emulator.Memory[0x9F27] = 0x04;     // hit not yet set in init.

        emulator.A = 0x04;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta ISR
                stp",
                emulator);

        Assert.AreEqual(AFLOW, emulator.Memory[0x9F27]);
        Assert.IsFalse(emulator.Vera.Interrupt_Vsync_Hit);
        Assert.IsFalse(emulator.Vera.Interrupt_Line_Hit);
        Assert.IsFalse(emulator.Vera.Interrupt_SpCol_Hit);
    }

    [TestMethod]
    public async Task Clear_Spcol_Preserve()
    {
        var emulator = new Emulator();

        emulator.Vera.Interrupt_Vsync_Hit = true;
        emulator.Vera.Interrupt_Line_Hit = true;
        emulator.Vera.Interrupt_SpCol_Hit = true;
        emulator.Memory[0x9F27] = 0xff;     // hit not yet set in init.

        emulator.A = 0x04;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta ISR
                stp",
                emulator);

        Assert.AreEqual(0xfb, emulator.Memory[0x9F27]);
        Assert.IsTrue(emulator.Vera.Interrupt_Vsync_Hit);
        Assert.IsTrue(emulator.Vera.Interrupt_Line_Hit);
        Assert.IsFalse(emulator.Vera.Interrupt_SpCol_Hit);
    }

    [TestMethod]
    public async Task Clear_All()
    {
        var emulator = new Emulator();

        emulator.Vera.Interrupt_Vsync_Hit = true;
        emulator.Vera.Interrupt_Line_Hit = true;
        emulator.Vera.Interrupt_SpCol_Hit = true;
        emulator.Memory[0x9F27] = 0xff;     // hit not yet set in init.

        emulator.A = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta ISR
                stp",
                emulator);

        Assert.AreEqual(0xf8, emulator.Memory[0x9F27]);
        Assert.IsFalse(emulator.Vera.Interrupt_Vsync_Hit);
        Assert.IsFalse(emulator.Vera.Interrupt_Line_Hit);
        Assert.IsFalse(emulator.Vera.Interrupt_SpCol_Hit);
    }


    [TestMethod]
    public async Task Clear_All_NothingSet()
    {
        var emulator = new Emulator();

        emulator.Vera.Interrupt_Vsync_Hit = false;
        emulator.Vera.Interrupt_Line_Hit = false;
        emulator.Vera.Interrupt_SpCol_Hit = false;
        emulator.Memory[0x9F27] = 0x00;     // hit not yet set in init.

        emulator.A = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta ISR
                stp",
                emulator);

        Assert.AreEqual(AFLOW, emulator.Memory[0x9F27]);
        Assert.IsFalse(emulator.Vera.Interrupt_Vsync_Hit);
        Assert.IsFalse(emulator.Vera.Interrupt_Line_Hit);
        Assert.IsFalse(emulator.Vera.Interrupt_SpCol_Hit);
    }
}