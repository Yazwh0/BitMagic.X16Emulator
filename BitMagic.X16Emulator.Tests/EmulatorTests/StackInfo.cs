using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Core;

[TestClass]
public class StackInfo
{
    [TestMethod]
    public async Task PHA_Normal()
    {
        var emulator = new Emulator();

        emulator.A = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                pha
                stp",
                emulator);
        emulator.AssertState(stackPointer: 0x1fc);
        Assert.AreEqual((uint)0x810, emulator.StackInfo[253]);
    }

    [TestMethod]
    public async Task PHA_RamBank()
    {
        var emulator = new Emulator();

        emulator.A = 0xff;
        emulator.Memory[0x00] = 0x0a; // ram bank

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                pha
                stp",
                emulator);
        emulator.AssertState(stackPointer: 0x1fc);
        Assert.AreEqual((uint)0x0a000810, emulator.StackInfo[253]);
    }

    [TestMethod]
    public async Task PHA_RomBank()
    {
        var emulator = new Emulator();

        emulator.A = 0xff;
        emulator.Memory[0x01] = 0x0f; // rom bank

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                pha
                stp",
                emulator);
        emulator.AssertState(stackPointer: 0x1fc);
        Assert.AreEqual((uint)0x000f0810, emulator.StackInfo[253]);
    }

    [TestMethod]
    public async Task PHA_BothBank()
    {
        var emulator = new Emulator();

        emulator.A = 0xff;
        emulator.Memory[0x00] = 0x7f; // ram bank
        emulator.Memory[0x01] = 0x19; // rom bank

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                pha
                stp",
                emulator);
        emulator.AssertState(stackPointer: 0x1fc);
        Assert.AreEqual((uint)0x7f190810, emulator.StackInfo[253]);
    }

    [TestMethod]
    public async Task PHX_Normal()
    {
        var emulator = new Emulator();

        emulator.X = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                phx
                stp",
                emulator);
        emulator.AssertState(stackPointer: 0x1fc);
        Assert.AreEqual((uint)0x810, emulator.StackInfo[253]);
    }

    [TestMethod]
    public async Task PHX_RamBank()
    {
        var emulator = new Emulator();

        emulator.A = 0xff;
        emulator.Memory[0x00] = 0x0a; // ram bank

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                phx
                stp",
                emulator);
        emulator.AssertState(stackPointer: 0x1fc);
        Assert.AreEqual((uint)0x0a000810, emulator.StackInfo[253]);
    }

    [TestMethod]
    public async Task PHX_RomBank()
    {
        var emulator = new Emulator();

        emulator.X = 0xff;
        emulator.Memory[0x01] = 0x0f; // rom bank

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                phx
                stp",
                emulator);
        emulator.AssertState(stackPointer: 0x1fc);
        Assert.AreEqual((uint)0x000f0810, emulator.StackInfo[253]);
    }

    [TestMethod]
    public async Task PHX_BothBank()
    {
        var emulator = new Emulator();

        emulator.X = 0xff;
        emulator.Memory[0x00] = 0x7f; // ram bank
        emulator.Memory[0x01] = 0x19; // rom bank

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                phx
                stp",
                emulator);
        emulator.AssertState(stackPointer: 0x1fc);
        Assert.AreEqual((uint)0x7f190810, emulator.StackInfo[253]);
    }

    [TestMethod]
    public async Task PHY_Normal()
    {
        var emulator = new Emulator();

        emulator.Y = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                phy
                stp",
                emulator);
        emulator.AssertState(stackPointer: 0x1fc);
        Assert.AreEqual((uint)0x810, emulator.StackInfo[253]);
    }

    [TestMethod]
    public async Task PHY_RamBank()
    {
        var emulator = new Emulator();

        emulator.Y = 0xff;
        emulator.Memory[0x00] = 0x0a; // ram bank

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                phy
                stp",
                emulator);
        emulator.AssertState(stackPointer: 0x1fc);
        Assert.AreEqual((uint)0x0a000810, emulator.StackInfo[253]);
    }

    [TestMethod]
    public async Task PHY_RomBank()
    {
        var emulator = new Emulator();

        emulator.Y = 0xff;
        emulator.Memory[0x01] = 0x0f; // rom bank

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                phy
                stp",
                emulator);
        emulator.AssertState(stackPointer: 0x1fc);
        Assert.AreEqual((uint)0x000f0810, emulator.StackInfo[253]);
    }

    [TestMethod]
    public async Task PHY_BothBank()
    {
        var emulator = new Emulator();

        emulator.Y = 0xff;
        emulator.Memory[0x00] = 0x7f; // ram bank
        emulator.Memory[0x01] = 0x19; // rom bank

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                phy
                stp",
                emulator);
        emulator.AssertState(stackPointer: 0x1fc);
        Assert.AreEqual((uint)0x7f190810, emulator.StackInfo[253]);
    }

    [TestMethod]
    public async Task PHP_Normal()
    {
        var emulator = new Emulator();

        emulator.Y = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                php
                stp",
                emulator);
        emulator.AssertState(stackPointer: 0x1fc);
        Assert.AreEqual((uint)0x810, emulator.StackInfo[253]);
    }

    [TestMethod]
    public async Task PHP_RamBank()
    {
        var emulator = new Emulator();

        emulator.Y = 0xff;
        emulator.Memory[0x00] = 0x0a; // ram bank

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                php
                stp",
                emulator);
        emulator.AssertState(stackPointer: 0x1fc);
        Assert.AreEqual((uint)0x0a000810, emulator.StackInfo[253]);
    }

    [TestMethod]
    public async Task PHP_RomBank()
    {
        var emulator = new Emulator();

        emulator.Y = 0xff;
        emulator.Memory[0x01] = 0x0f; // rom bank

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                php
                stp",
                emulator);
        emulator.AssertState(stackPointer: 0x1fc);
        Assert.AreEqual((uint)0x000f0810, emulator.StackInfo[253]);
    }

    [TestMethod]
    public async Task PHP_BothBank()
    {
        var emulator = new Emulator();

        emulator.Y = 0xff;
        emulator.Memory[0x00] = 0x7f; // ram bank
        emulator.Memory[0x01] = 0x19; // rom bank

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                php
                stp",
                emulator);
        emulator.AssertState(stackPointer: 0x1fc);
        Assert.AreEqual((uint)0x7f190810, emulator.StackInfo[253]);
    }

    [TestMethod]
    public async Task JSR_Normal()
    {
        var emulator = new Emulator();

        emulator.Y = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                jsr proc
                stp
            .proc:
                stp
                ",
                emulator);
        emulator.AssertState(stackPointer: 0x1fb);
        Assert.AreEqual((uint)0x810, emulator.StackInfo[253]);
        Assert.AreEqual((uint)0x810, emulator.StackInfo[252]);
    }

    [TestMethod]
    public async Task JSR_RamBank()
    {
        var emulator = new Emulator();

        emulator.Y = 0xff;
        emulator.Memory[0x00] = 0x0a; // ram bank

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                jsr proc
                stp
            .proc:
                stp
                ",
                emulator);
        emulator.AssertState(stackPointer: 0x1fb);
        Assert.AreEqual((uint)0x0a000810, emulator.StackInfo[253]);
        Assert.AreEqual((uint)0x0a000810, emulator.StackInfo[252]);
    }

    [TestMethod]
    public async Task JSR_RomBank()
    {
        var emulator = new Emulator();

        emulator.Y = 0xff;
        emulator.Memory[0x01] = 0x0f; // rom bank

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                jsr proc
                stp
            .proc:
                stp
                ",
                emulator);
        emulator.AssertState(stackPointer: 0x1fb);
        Assert.AreEqual((uint)0x000f0810, emulator.StackInfo[253]);
        Assert.AreEqual((uint)0x000f0810, emulator.StackInfo[252]);
    }

    [TestMethod]
    public async Task JSR_BothBank()
    {
        var emulator = new Emulator();

        emulator.Y = 0xff;
        emulator.Memory[0x00] = 0x7f; // ram bank
        emulator.Memory[0x01] = 0x19; // rom bank

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                jsr proc
                stp
            .proc:
                stp
                ",
                emulator);
        emulator.AssertState(stackPointer: 0x1fb);
        Assert.AreEqual((uint)0x7f190810, emulator.StackInfo[253]);
        Assert.AreEqual((uint)0x7f190810, emulator.StackInfo[252]);
    }

    [TestMethod]
    public async Task BRK_Normal()
    {
        var emulator = new Emulator();

        emulator.Nmi = false;

        emulator.RomBank[0x3ffa] = 0x00;
        emulator.RomBank[0x3ffb] = 0x09;

        emulator.Brk_Causes_Stop = false;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                brk
                stp
                .org $900
                stp
                ",
                emulator, dontChangeEmulatorOptions: true);

        emulator.AssertState(stackPointer: 0x1fa);
        Assert.AreEqual((uint)0x810, emulator.StackInfo[253]);
        Assert.AreEqual((uint)0x810, emulator.StackInfo[252]);
        Assert.AreEqual((uint)0x810, emulator.StackInfo[251]);
    }

    [TestMethod]
    public async Task BRK_RamBank()
    {
        var emulator = new Emulator();

        emulator.Memory[0x00] = 0x0a; // ram bank
        emulator.Nmi = false;

        emulator.RomBank[0x3ffa] = 0x00;
        emulator.RomBank[0x3ffb] = 0x09;

        emulator.Brk_Causes_Stop = false;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                brk
                stp
                .org $900
                stp
                ",
                emulator, dontChangeEmulatorOptions: true);
        emulator.AssertState(stackPointer: 0x1fa);
        Assert.AreEqual((uint)0x0a000810, emulator.StackInfo[253]);
        Assert.AreEqual((uint)0x0a000810, emulator.StackInfo[252]);
        Assert.AreEqual((uint)0x0a000810, emulator.StackInfo[251]);
    }

    [TestMethod]
    public async Task BRK_RomBank()
    {
        var emulator = new Emulator();

        emulator.Memory[0x01] = 0x0f; // rom bank

        emulator.Nmi = false;

        emulator.RomBank[0x3ffa + 0x4000 * 0x0f] = 0x00;
        emulator.RomBank[0x3ffb + 0x4000 * 0x0f] = 0x09;

        emulator.Brk_Causes_Stop = false;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                brk
                stp
                .org $900
                stp
                ",
                emulator, dontChangeEmulatorOptions: true);
        emulator.AssertState(stackPointer: 0x1fa);
        Assert.AreEqual((uint)0x000f0810, emulator.StackInfo[253]);
        Assert.AreEqual((uint)0x000f0810, emulator.StackInfo[252]);
        Assert.AreEqual((uint)0x000f0810, emulator.StackInfo[251]);
    }

    [TestMethod]
    public async Task BRK_BothBank()
    {
        var emulator = new Emulator();

        emulator.Memory[0x00] = 0x7f; // ram bank
        emulator.Memory[0x01] = 0x19; // rom bank

        emulator.Nmi = false;

        emulator.RomBank[0x3ffa + 0x4000 * 0x19] = 0x00;
        emulator.RomBank[0x3ffb + 0x4000 * 0x19] = 0x09;

        emulator.Brk_Causes_Stop = false;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                brk
                stp
                .org $900
                stp
                ",
                emulator, dontChangeEmulatorOptions: true);
        emulator.AssertState(stackPointer: 0x1fa);
        Assert.AreEqual((uint)0x7f190810, emulator.StackInfo[253]);
        Assert.AreEqual((uint)0x7f190810, emulator.StackInfo[252]);
        Assert.AreEqual((uint)0x7f190810, emulator.StackInfo[251]);
    }
}