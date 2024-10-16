﻿using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Core;

[TestClass]
public class EmulatorTests
{
    [TestMethod]
    public void SetMemory()
    {
        var emulator = new Emulator();

        emulator.Memory[0x810] = 0xdb;

        Assert.AreEqual(0xdb, emulator.Memory[0x810]);
    }

    [TestMethod]
    public async Task CarryFlag()
    {
        var emulator = new Emulator();

        emulator.Carry = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0xdb, emulator.Memory[0x810]);

        // emulation
        emulator.AssertState(0x00, 0x00, 0x00, 0x811, 0);
        emulator.AssertFlags(false, false, false, true);
    }

    [TestMethod]
    public async Task ZeroFlag()
    {
        var emulator = new Emulator();

        emulator.Zero = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0xdb, emulator.Memory[0x810]);

        // emulation
        emulator.AssertState(0x00, 0x00, 0x00, 0x811, 0);
        emulator.AssertFlags(true, false, false, false);
    }

    [TestMethod]
    public async Task NegativeFlag()
    {
        var emulator = new Emulator();

        emulator.Negative = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0xdb, emulator.Memory[0x810]);

        // emulation
        emulator.AssertState(0x00, 0x00, 0x00, 0x811, 0);
        emulator.AssertFlags(false, true, false, false);
    }

    [TestMethod]
    public async Task OverflowFlag()
    {
        var emulator = new Emulator();

        emulator.Overflow = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0xdb, emulator.Memory[0x810]);

        // emulation
        emulator.AssertState(0x00, 0x00, 0x00, 0x811, 0);
        emulator.AssertFlags(false, false, true, false);
    }

    [TestMethod]
    public async Task Interrupt_Set()
    {
        var emulator = new Emulator();

        emulator.InterruptHit = InterruptSource.Vsync;
        emulator.InterruptMask = InterruptSource.Vsync;

        // set interrupt vector to $900
        emulator.RomBank[0x3ffe] = 0x00;
        emulator.RomBank[0x3fff] = 0x09;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp
                .org $900
                stp",
                emulator);

        // emulation
        emulator.AssertState(0x00, 0x00, 0x00, 0x901, 7, 0x1fd - 3);
        emulator.AssertFlags(Interrupt: true, InterruptDisable: true);
    }

    [TestMethod]
    public async Task Interrupt_ClearDecimal()
    {
        var emulator = new Emulator();

        emulator.InterruptHit = InterruptSource.Vsync;
        emulator.InterruptMask = InterruptSource.Vsync;
        emulator.Decimal = true;

        // set interrupt vector to $900
        emulator.RomBank[0x3ffe] = 0x00;
        emulator.RomBank[0x3fff] = 0x09;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp
                .org $900
                stp",
                emulator);

        // emulation
        emulator.AssertState(0x00, 0x00, 0x00, 0x901, 7, 0x1fd - 3);
        emulator.AssertFlags(Interrupt: true, Decimal: false, InterruptDisable: true);
    }


    //[TestMethod]
    //public async Task Interrupt_SetAndReturn()
    //{
    //    var emulator = new Emulator();

    //    emulator.Interrupt = true;

    //    // set interrupt vector to $900
    //    emulator.RomBank[0x3ffe] = 0x00;
    //    emulator.RomBank[0x3fff] = 0x09;

    //    await X16TestHelper.Emulate(@"
    //            .machine CommanderX16R40
    //            .org $810
    //            stp
    //            .org $900
    //            rti",
    //            emulator);

    //    // emulation
    //    emulator.AssertState(0x00, 0x00, 0x00, 0x811, 7 + 6, 0x1fd);
    //    emulator.AssertFlags(Interrupt: false);
    //}

    //[TestMethod]
    //public async Task Interrupt_SetAndReturn_Decimal()
    //{
    //    var emulator = new Emulator();

    //    emulator.Interrupt = true;
    //    emulator.Decimal = true;

    //    // set interrupt vector to $900
    //    emulator.RomBank[0x3ffe] = 0x00;
    //    emulator.RomBank[0x3fff] = 0x09;

    //    await X16TestHelper.Emulate(@"
    //            .machine CommanderX16R40
    //            .org $810
    //            stp
    //            .org $900
    //            rti",
    //            emulator);

    //    // emulation
    //    emulator.AssertState(0x00, 0x00, 0x00, 0x811, 7 + 6, 0x1fd);
    //    emulator.AssertFlags(Interrupt: false, Decimal: true);
    //}

    [TestMethod]
    public async Task Interrupt_Notset()
    {
        var emulator = new Emulator();

        emulator.InterruptHit = InterruptSource.Vsync;
        emulator.InterruptMask = InterruptSource.Vsync;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0xdb, emulator.Memory[0x810]);

        // emulation
        emulator.AssertState(0x00, 0x00, 0x00, 0x811, 0);
        emulator.AssertFlags(Interrupt: false);
    }

    [TestMethod]
    public async Task Interrupt_Set_RomChange()
    {
        var emulator = new Emulator();

        emulator.InterruptHit = InterruptSource.Vsync;
        emulator.InterruptMask = InterruptSource.Vsync;

        // always uses rom bank 0 for interrupts

        emulator.RomBank[0x4000 * 5 + 0x3ffe] = 0x00;
        emulator.RomBank[0x4000 * 5 + 0x3fff] = 0x09;
        emulator.RomBank[0x4000 * 0 + 0x3ffe] = 0x00;
        emulator.RomBank[0x4000 * 0 + 0x3fff] = 0x0a;
        emulator.Memory[0x01] = 0x05;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp
                .org $900
                stp
                .org $a00
                stp",
                emulator);

        // emulation
        emulator.AssertState(0x00, 0x00, 0x00, 0xa01, stackPointer: 0x1fd - 3);
        emulator.AssertFlags(Interrupt: true, InterruptDisable: true);
    }

    //[TestMethod]
    //public async Task Interrupt_SetAndReturn_RomChange()
    //{
    //    var emulator = new Emulator();

    //    emulator.Interrupt = true;

    //    // set interrupt vector to $900
    //    emulator.RomBank[0x4000 * 5 + 0x3ffe] = 0x00;
    //    emulator.RomBank[0x4000 * 5 + 0x3fff] = 0x09;
    //    emulator.Memory[0x01] = 0x05;

    //    await X16TestHelper.Emulate(@"
    //            .machine CommanderX16R40
    //            .org $810
    //            stp
    //            .org $900
    //            rti",
    //            emulator);

    //    // emulation
    //    emulator.AssertState(0x00, 0x00, 0x00, 0x811, stackPointer: 0x1fd);
    //    emulator.AssertFlags(Interrupt: false);
    //}

    [TestMethod]
    public async Task Nmi_Precidence()
    {
        var emulator = new Emulator();

        emulator.Nmi = true;
        emulator.InterruptHit = InterruptSource.Vsync;
        emulator.InterruptMask = InterruptSource.Vsync;

        // set interrupt vector to $900
        emulator.RomBank[0x3ffa] = 0x00;
        emulator.RomBank[0x3ffb] = 0x09;
        emulator.RomBank[0x3ffe] = 0x00;
        emulator.RomBank[0x3fff] = 0x0a;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp
                .org $900
                stp
                .org $a00
                stp
                ",
                emulator);

        // emulation
        emulator.AssertState(0x00, 0x00, 0x00, 0x901, 7, 0x1fd - 3);
        emulator.AssertFlags(Interrupt: true, Nmi: true, InterruptDisable: true);
    }

    [TestMethod]
    public async Task Nmi_Set()
    {
        var emulator = new Emulator();

        emulator.Nmi = true;

        // set interrupt vector to $900
        emulator.RomBank[0x3ffa] = 0x00;
        emulator.RomBank[0x3ffb] = 0x09;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp
                .org $900
                stp",
                emulator);

        // emulation
        emulator.AssertState(0x00, 0x00, 0x00, 0x901, 7, 0x1fd - 3);
        emulator.AssertFlags(Nmi: true, InterruptDisable: true); // nmi doesn't get cleared
    }

    [TestMethod]
    public async Task Nmi_Set_IgnoreInterruptDisable()
    {
        var emulator = new Emulator();

        emulator.Nmi = true;
        emulator.InterruptDisable = true;

        // set interrupt vector to $900
        emulator.RomBank[0x3ffa] = 0x00;
        emulator.RomBank[0x3ffb] = 0x09;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp
                .org $900
                stp",
                emulator);

        // emulation
        emulator.AssertState(0x00, 0x00, 0x00, 0x901, 7, 0x1fd - 3);
        emulator.AssertFlags(Nmi: true, InterruptDisable: true); // nmi doesn't get cleared
    }

    // Cant test as VIA clears NMI when it checks
    //[TestMethod]
    //public async Task Nmi_SetAndReturn()
    //{
    //    var emulator = new Emulator();

    //    emulator.Nmi = true;

    //    // set interrupt vector to $900
    //    emulator.RomBank[0x3ffa] = 0x00;
    //    emulator.RomBank[0x3ffb] = 0x09;

    //    await X16TestHelper.Emulate(@"
    //            .machine CommanderX16R40
    //            .org $810
    //            stp
    //            .org $900
    //            rti",
    //            emulator);

    //    // emulation
    //    emulator.AssertState(0x00, 0x00, 0x00, 0x811, 7 + 6, 0x1fd);
    //    emulator.AssertFlags(Nmi: true);
    //}

    [TestMethod]
    public async Task Nmi_Notset()
    {
        var emulator = new Emulator();

        emulator.Nmi = false;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp",
                emulator);

        // compilation
        Assert.AreEqual(0xdb, emulator.Memory[0x810]);

        // emulation
        emulator.AssertState(0x00, 0x00, 0x00, 0x811, 0);
        emulator.AssertFlags(Nmi: false);
    }

    [TestMethod]
    public async Task Nmi_Set_RomChange()
    {
        var emulator = new Emulator();

        emulator.Nmi = true;

        emulator.RomBank[0x4000 * 0 + 0x3ffa] = 0x00;
        emulator.RomBank[0x4000 * 0 + 0x3ffb] = 0x0a;
        emulator.RomBank[0x4000 * 5 + 0x3ffa] = 0x00;
        emulator.RomBank[0x4000 * 5 + 0x3ffb] = 0x09;
        emulator.Memory[0x01] = 0x05;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp
                .org $900
                stp
                .org $a00
                stp",
                emulator);

        // emulation
        emulator.AssertState(0x00, 0x00, 0x00, 0xa01, stackPointer: 0x1fd - 3);
        emulator.AssertFlags(Nmi: true, InterruptDisable: true);
    }

    // Cant test as VIA clears NMI when it checks
    //[TestMethod]
    //public async Task Nmi_SetAndReturn_RomChange()
    //{
    //    var emulator = new Emulator();

    //    emulator.Nmi = true;

    //    // set interrupt vector to $900
    //    emulator.RomBank[0x4000 * 5 + 0x3ffa] = 0x00;
    //    emulator.RomBank[0x4000 * 5 + 0x3ffb] = 0x09;
    //    emulator.Memory[0x01] = 0x05;

    //    await X16TestHelper.Emulate(@"
    //            .machine CommanderX16R40
    //            .org $810
    //            stp
    //            .org $900
    //            rti",
    //            emulator);

    //    // emulation
    //    emulator.AssertState(0x00, 0x00, 0x00, 0x811, stackPointer: 0x1fd);
    //    emulator.AssertFlags(Nmi: true);
    //}

    [TestMethod]
    public async Task Memory_Bank_Init()
    {
        var emulator = new Emulator();

        emulator.RamBank[0x2000] = 0x02;
        emulator.RamBank[0x2000 + 0x1fff] = 0x04;
        emulator.Memory[0x00] = 0x01;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810                
                stp",
                emulator);

        Assert.AreEqual(0x02, emulator.Memory[0xa000]);
        Assert.AreEqual(0x04, emulator.Memory[0xa000 + 0x1fff]);
    }
}