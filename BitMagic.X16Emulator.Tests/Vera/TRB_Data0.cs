﻿using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

[TestClass]
public class TRB_Data0
{
    [TestMethod]
    public async Task Abs_Step0()
    {
        var emulator = new Emulator();

        emulator.A = 0x02;
        emulator.Vera.Vram[0x0000] = 0x03;
        emulator.Vera.Data0_Step = 0;
        emulator.Vera.Data0_Address = 0x0000;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                trb DATA0
                stp",
                emulator);

        Assert.AreEqual(0x00000, emulator.Vera.Data0_Address);
        Assert.AreEqual(0x01, emulator.Vera.Vram[0x0000]);
        Assert.AreEqual(0x00, emulator.Vera.Vram[0x0001]);
        Assert.AreEqual(0x01, emulator.Memory[0x9F23]);

        Assert.AreEqual(0x00, emulator.Memory[0x9F20]);
        Assert.AreEqual(0x00, emulator.Memory[0x9F21]);
        Assert.AreEqual(0x00, emulator.Memory[0x9F22]);
    }

    [TestMethod]
    public async Task Abs_Step0_Zero()
    {
        var emulator = new Emulator();

        emulator.A = 0xff;
        emulator.Vera.Vram[0x0000] = 0x01;
        emulator.Vera.Data0_Step = 0;
        emulator.Vera.Data0_Address = 0x0000;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                trb DATA0
                stp",
                emulator);

        emulator.AssertFlags(Zero: true);
        Assert.AreEqual(0x00000, emulator.Vera.Data0_Address);
        Assert.AreEqual(0x00, emulator.Vera.Vram[0x0000]);
        Assert.AreEqual(0x00, emulator.Vera.Vram[0x0001]);
        Assert.AreEqual(0x00, emulator.Memory[0x9F23]);

        Assert.AreEqual(0x00, emulator.Memory[0x9F20]);
        Assert.AreEqual(0x00, emulator.Memory[0x9F21]);
        Assert.AreEqual(0x00, emulator.Memory[0x9F22]);
    }

    [TestMethod]
    public async Task Abs_Step1()
    {
        var emulator = new Emulator();

        emulator.A = 0x02;
        emulator.Vera.Vram[0x0000] = 0x03;
        emulator.Vera.Vram[0x0001] = 0xaa;
        emulator.Vera.Vram[0x0002] = 0xee;
        emulator.Vera.Vram[0x0003] = 0xff;
        emulator.Vera.Data0_Step = 1;
        emulator.Vera.Data0_Address = 0x0000;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                trb DATA0
                stp",
                emulator);

        Assert.AreEqual(0x00003, emulator.Vera.Data0_Address);
        Assert.AreEqual(0x03, emulator.Vera.Vram[0x0000]);
        Assert.AreEqual(0xaa, emulator.Vera.Vram[0x0001]);
        Assert.AreEqual(0x01, emulator.Vera.Vram[0x0002]);
        Assert.AreEqual(0xff, emulator.Memory[0x9F23]);

        Assert.AreEqual(0x03, emulator.Memory[0x9F20]);
        Assert.AreEqual(0x00, emulator.Memory[0x9F21]);
        Assert.AreEqual(0x10, emulator.Memory[0x9F22]);
    }

    [TestMethod]
    public async Task Abs_Step1_Zero()
    {
        var emulator = new Emulator();

        emulator.A = 0xff;
        emulator.Vera.Vram[0x0000] = 0x01;
        emulator.Vera.Data0_Step = 1;
        emulator.Vera.Data0_Address = 0x0000;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                trb DATA0
                stp",
                emulator);

        emulator.AssertFlags(Zero: true);
        Assert.AreEqual(0x00003, emulator.Vera.Data0_Address);
        Assert.AreEqual(0x01, emulator.Vera.Vram[0x0000]);
        Assert.AreEqual(0x00, emulator.Vera.Vram[0x0001]);
        Assert.AreEqual(0x00, emulator.Memory[0x9F23]);

        Assert.AreEqual(0x03, emulator.Memory[0x9F20]);
        Assert.AreEqual(0x00, emulator.Memory[0x9F21]);
        Assert.AreEqual(0x10, emulator.Memory[0x9F22]);
    }
}
