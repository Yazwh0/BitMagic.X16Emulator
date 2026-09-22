using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Via;

[TestClass]
public class InerruptFlags
{
    [TestMethod]
    public async Task Interrupt_Timer1()
    {
        var emulator = new Emulator();
        emulator.Via.Interrupt_Timer1 = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp",
                emulator);

        Assert.IsTrue(emulator.Via.Interrupt_Timer1);
        Assert.AreEqual(0xc0, emulator.Memory[0x9f0e]);
    }

    [TestMethod]
    public async Task Interrupt_Timer2()
    {
        var emulator = new Emulator();
        emulator.Via.Interrupt_Timer2 = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp",
                emulator);

        Assert.IsTrue(emulator.Via.Interrupt_Timer2);
        Assert.AreEqual(0xa0, emulator.Memory[0x9f0e]);
    }

    [TestMethod]
    public async Task Interrupt_Cb1()
    {
        var emulator = new Emulator();
        emulator.Via.Interrupt_Cb1 = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp",
                emulator);

        Assert.IsTrue(emulator.Via.Interrupt_Cb1);
        Assert.AreEqual(0x90, emulator.Memory[0x9f0e]);
    }

    [TestMethod]
    public async Task Interrupt_Cb2()
    {
        var emulator = new Emulator();
        emulator.Via.Interrupt_Cb2 = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp",
                emulator);

        Assert.IsTrue(emulator.Via.Interrupt_Cb2);
        Assert.AreEqual(0x88, emulator.Memory[0x9f0e]);
    }

    [TestMethod]
    public async Task Interrupt_ShiftRegister()
    {
        var emulator = new Emulator();
        emulator.Via.Interrupt_ShiftRegister = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp",
                emulator);

        Assert.IsTrue(emulator.Via.Interrupt_ShiftRegister);
        Assert.AreEqual(0x84, emulator.Memory[0x9f0e]);
    }

    [TestMethod]
    public async Task Interrupt_Ca1()
    {
        var emulator = new Emulator();
        emulator.Via.Interrupt_Ca1= true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp",
                emulator);

        Assert.IsTrue(emulator.Via.Interrupt_Ca1);
        Assert.AreEqual(0x82, emulator.Memory[0x9f0e]);
    }

    [TestMethod]
    public async Task Interrupt_Ca2()
    {
        var emulator = new Emulator();
        emulator.Via.Interrupt_Ca2 = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp",
                emulator);

        Assert.IsTrue(emulator.Via.Interrupt_Ca2);
        Assert.AreEqual(0x81, emulator.Memory[0x9f0e]);
    }

    [TestMethod]
    public async Task Interrupt_Set_Ca2()
    {
        var emulator = new Emulator();
        emulator.A = 0b10000001;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta V_IER
                stp",
                emulator);

        Assert.IsTrue(emulator.Via.Interrupt_Ca2);
        Assert.AreEqual(0x81, emulator.Memory[0x9f0e]);
    }

    [TestMethod]
    public async Task Interrupt_Set_Ca1()
    {
        var emulator = new Emulator();
        emulator.A = 0b10000010;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta V_IER
                stp",
                emulator);

        Assert.IsTrue(emulator.Via.Interrupt_Ca1);
        Assert.AreEqual(0x82, emulator.Memory[0x9f0e]);
    }

    [TestMethod]
    public async Task Interrupt_Set_ShiftRegister()
    {
        var emulator = new Emulator();
        emulator.A = 0b10000100;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta V_IER
                stp",
                emulator);

        Assert.IsTrue(emulator.Via.Interrupt_ShiftRegister);
        Assert.AreEqual(0x84, emulator.Memory[0x9f0e]);
    }

    [TestMethod]
    public async Task Interrupt_Set_Cb2()
    {
        var emulator = new Emulator();
        emulator.A = 0b10001000;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta V_IER
                stp",
                emulator);

        Assert.IsTrue(emulator.Via.Interrupt_Cb2);
        Assert.AreEqual(0x88, emulator.Memory[0x9f0e]);
    }

    [TestMethod]
    public async Task Interrupt_Set_Cb1()
    {
        var emulator = new Emulator();
        emulator.A = 0b10010000;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta V_IER
                stp",
                emulator);

        Assert.IsTrue(emulator.Via.Interrupt_Cb1);
        Assert.AreEqual(0x90, emulator.Memory[0x9f0e]);
    }

    [TestMethod]
    public async Task Interrupt_Set_Timer2()
    {
        var emulator = new Emulator();
        emulator.A = 0b10100000;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sei
                sta V_IER
                stp",
                emulator);

        Assert.IsTrue(emulator.Via.Interrupt_Timer2);
        Assert.AreEqual(0xa0, emulator.Memory[0x9f0e]);
    }

    [TestMethod]
    public async Task Interrupt_Set_Timer1()
    {
        var emulator = new Emulator();
        emulator.A = 0b11000000;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sei
                sta V_IER
                stp",
                emulator);

        Assert.IsTrue(emulator.Via.Interrupt_Timer1);
        Assert.AreEqual(0xc0, emulator.Memory[0x9f0e]);
    }

    [TestMethod]
    public async Task Interrupt_UnSet_Ca2()
    {
        var emulator = new Emulator();
        emulator.A = 0b00000001;
        emulator.Via.Interrupt_Ca2 = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta V_IER
                stp",
                emulator);

        Assert.IsFalse(emulator.Via.Interrupt_Ca2);
        Assert.AreEqual(0x80, emulator.Memory[0x9f0e]);
    }

    [TestMethod]
    public async Task Interrupt_UnSet_Ca1()
    {
        var emulator = new Emulator();
        emulator.A = 0b00000010;
        emulator.Via.Interrupt_Ca1 = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta V_IER
                stp",
                emulator);

        Assert.IsFalse(emulator.Via.Interrupt_Ca1);
        Assert.AreEqual(0x80, emulator.Memory[0x9f0e]);
    }

    [TestMethod]
    public async Task Interrupt_UnSet_ShiftRegister()
    {
        var emulator = new Emulator();
        emulator.A = 0b00000100;
        emulator.Via.Interrupt_ShiftRegister = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta V_IER
                stp",
                emulator);

        Assert.IsFalse(emulator.Via.Interrupt_ShiftRegister);
        Assert.AreEqual(0x80, emulator.Memory[0x9f0e]);
    }

    [TestMethod]
    public async Task Interrupt_UnSet_Cb2()
    {
        var emulator = new Emulator();
        emulator.A = 0b00001000;
        emulator.Via.Interrupt_Cb2 = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta V_IER
                stp",
                emulator);

        Assert.IsFalse(emulator.Via.Interrupt_Cb2);
        Assert.AreEqual(0x80, emulator.Memory[0x9f0e]);
    }

    [TestMethod]
    public async Task Interrupt_UnSet_Cb1()
    {
        var emulator = new Emulator();
        emulator.A = 0b00010000;
        emulator.Via.Interrupt_Cb1 = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta V_IER
                stp",
                emulator);

        Assert.IsFalse(emulator.Via.Interrupt_Cb1);
        Assert.AreEqual(0x80, emulator.Memory[0x9f0e]);
    }

    [TestMethod]
    public async Task Interrupt_UnSet_Timer2()
    {
        var emulator = new Emulator();
        emulator.A = 0b00100000;
        emulator.Via.Interrupt_Timer2 = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta V_IER
                stp",
                emulator);

        Assert.IsFalse(emulator.Via.Interrupt_Timer2);
        Assert.AreEqual(0x80, emulator.Memory[0x9f0e]);
    }

    [TestMethod]
    public async Task Interrupt_UnSet_Timer1()
    {
        var emulator = new Emulator();
        emulator.A = 0b01000000;
        emulator.Via.Interrupt_Timer1 = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sta V_IER
                stp",
                emulator);

        Assert.IsFalse(emulator.Via.Interrupt_Timer1);
        Assert.AreEqual(0x80, emulator.Memory[0x9f0e]);
    }

    // The setters used to be `Memory[0x9f0e] |= mask` unconditionally, which can only ever set a
    // bit - assigning false was a silent no-op. These exercise the property setter's own clear
    // path directly (no CPU write, no emulation run - unlike Interrupt_UnSet_* above, which goes
    // through the real `sta V_IER` write-trap instead), and check clearing one flag doesn't
    // disturb the others. No hardcoded memory byte value here (unlike the tests above): those run
    // via X16TestHelper.Emulate, which triggers via_init and always ORs in bit 0x80 first: these
    // don't run the emulator at all, so that baseline doesn't apply.

    [TestMethod]
    public void Interrupt_Timer1_SetterCanClearTheBit()
    {
        var emulator = new Emulator();
        emulator.Via.Interrupt_Timer1 = true;

        emulator.Via.Interrupt_Timer1 = false;

        Assert.IsFalse(emulator.Via.Interrupt_Timer1);
    }

    [TestMethod]
    public void Interrupt_Timer2_SetterCanClearTheBit()
    {
        var emulator = new Emulator();
        emulator.Via.Interrupt_Timer2 = true;

        emulator.Via.Interrupt_Timer2 = false;

        Assert.IsFalse(emulator.Via.Interrupt_Timer2);
    }

    [TestMethod]
    public void Interrupt_Cb1_SetterCanClearTheBit()
    {
        var emulator = new Emulator();
        emulator.Via.Interrupt_Cb1 = true;

        emulator.Via.Interrupt_Cb1 = false;

        Assert.IsFalse(emulator.Via.Interrupt_Cb1);
    }

    [TestMethod]
    public void Interrupt_Cb2_SetterCanClearTheBit()
    {
        var emulator = new Emulator();
        emulator.Via.Interrupt_Cb2 = true;

        emulator.Via.Interrupt_Cb2 = false;

        Assert.IsFalse(emulator.Via.Interrupt_Cb2);
    }

    [TestMethod]
    public void Interrupt_ShiftRegister_SetterCanClearTheBit()
    {
        var emulator = new Emulator();
        emulator.Via.Interrupt_ShiftRegister = true;

        emulator.Via.Interrupt_ShiftRegister = false;

        Assert.IsFalse(emulator.Via.Interrupt_ShiftRegister);
    }

    [TestMethod]
    public void Interrupt_Ca1_SetterCanClearTheBit()
    {
        var emulator = new Emulator();
        emulator.Via.Interrupt_Ca1 = true;

        emulator.Via.Interrupt_Ca1 = false;

        Assert.IsFalse(emulator.Via.Interrupt_Ca1);
    }

    [TestMethod]
    public void Interrupt_Ca2_SetterCanClearTheBit()
    {
        var emulator = new Emulator();
        emulator.Via.Interrupt_Ca2 = true;

        emulator.Via.Interrupt_Ca2 = false;

        Assert.IsFalse(emulator.Via.Interrupt_Ca2);
    }

    [TestMethod]
    public void Interrupt_Ca2_SetterClear_DoesNotDisturbOtherSetBits()
    {
        var emulator = new Emulator();
        emulator.Via.Interrupt_Ca1 = true;
        emulator.Via.Interrupt_Timer1 = true;
        emulator.Via.Interrupt_Ca2 = true;

        emulator.Via.Interrupt_Ca2 = false;

        Assert.IsFalse(emulator.Via.Interrupt_Ca2);
        Assert.IsTrue(emulator.Via.Interrupt_Ca1);
        Assert.IsTrue(emulator.Via.Interrupt_Timer1);
    }
}