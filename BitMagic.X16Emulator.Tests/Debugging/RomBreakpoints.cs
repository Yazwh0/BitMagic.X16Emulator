using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Debugging;

[TestClass]
public class RomBreakpoints
{
    [TestMethod]
    public async Task CurrentRom()
    {
        var emulator = new Emulator();

        emulator.Breakpoints[0xc001] = 1;
        emulator.Breakpoints[0x210000 + (0x4000 * 0) + 0x0001] = 1;

        emulator.RomBank[0x0000] = 0xea;
        emulator.RomBank[0x0001] = 0xea;
        emulator.RomBank[0x0002] = 0xea;
        emulator.RomBank[0x0003] = 0xdb;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                jmp $c000",
                emulator, expectedResult: Emulator.EmulatorResult.Breakpoint);

        emulator.AssertState(Pc: 0xc001);
    }

    [TestMethod]
    public async Task CurrentRomHigh()
    {
        var emulator = new Emulator();

        emulator.Breakpoints[0xfff1] = 1;
        emulator.Breakpoints[0x210000 + (0x4000 * 0) + 0x3ff1] = 1;

        emulator.RomBank[0x3ff0] = 0xea;
        emulator.RomBank[0x3ff1] = 0xea;
        emulator.RomBank[0x3ff2] = 0xea;
        emulator.RomBank[0x3ff3] = 0xdb;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                jmp $fff0",
                emulator, expectedResult: Emulator.EmulatorResult.Breakpoint);

        emulator.AssertState(Pc: 0xfff1);
    }

    [TestMethod]
    public async Task SwitchRom()
    {
        var emulator = new Emulator();

        emulator.Breakpoints[0x210000 + (0x4000 * 1) + 0x0001] = 1;

        emulator.RomBank[0x4000] = 0xea;
        emulator.RomBank[0x4001] = 0xea;
        emulator.RomBank[0x4002] = 0xea;
        emulator.RomBank[0x4003] = 0xdb;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #01
                sta $01
                jmp $c000",
                emulator, expectedResult: Emulator.EmulatorResult.Breakpoint);

        emulator.AssertState(Pc: 0xc001);
    }

    [TestMethod]
    public async Task SwitchRomHigh()
    {
        var emulator = new Emulator();

        emulator.Breakpoints[0x210000 + (0x4000 * 1) + 0x3ff1] = 1;

        emulator.RomBank[0x7ff0] = 0xea;
        emulator.RomBank[0x7ff1] = 0xea;
        emulator.RomBank[0x7ff2] = 0xea;
        emulator.RomBank[0x7ff3] = 0xdb;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #01
                sta $01
                jmp $fff0",
                emulator, expectedResult: Emulator.EmulatorResult.Breakpoint);

        emulator.AssertState(Pc: 0xfff1);
    }

    [TestMethod]
    public async Task SwitchRomBackNotFire()
    {
        var emulator = new Emulator();

        emulator.Breakpoints[0x210000 + (0x4000 * 1) + 0x0001] = 1;

        emulator.RomBank[0x0000] = 0xea;
        emulator.RomBank[0x0001] = 0xea;
        emulator.RomBank[0x0002] = 0xea;
        emulator.RomBank[0x0003] = 0xdb;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #01
                sta $01
                stz $01
                jmp $c000",
                emulator, expectedResult: Emulator.EmulatorResult.DebugOpCode);

        emulator.AssertState(Pc: 0xc004);
    }

    [TestMethod]
    public async Task SwitchRomBackNotFireHigh()
    {
        var emulator = new Emulator();

        emulator.Breakpoints[0x210000 + (0x4000 * 1) + 0xfff1] = 1;

        emulator.RomBank[0x3ff0] = 0xea;
        emulator.RomBank[0x3ff1] = 0xea;
        emulator.RomBank[0x3ff2] = 0xea;
        emulator.RomBank[0x3ff3] = 0xdb;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #01
                sta $01
                stz $01
                jmp $fff0",
                emulator, expectedResult: Emulator.EmulatorResult.DebugOpCode);

        emulator.AssertState(Pc: 0xfff4);
    }

    [TestMethod]
    public async Task SwitchRomNotFire()
    {
        var emulator = new Emulator();

        emulator.Breakpoints[0xc001] = 1;
        emulator.Breakpoints[0x210000 + (0x0000 * 0) + 0x0001] = 1;

        emulator.RomBank[0x0000] = 0xea;
        emulator.RomBank[0x0001] = 0xea;
        emulator.RomBank[0x0002] = 0xea;
        emulator.RomBank[0x0003] = 0xdb;

        emulator.RomBank[0x4000] = 0xea;
        emulator.RomBank[0x4001] = 0xea;
        emulator.RomBank[0x4002] = 0xea;
        emulator.RomBank[0x4003] = 0xdb;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #01
                sta $01

                jmp $c000",
                emulator, expectedResult: Emulator.EmulatorResult.DebugOpCode);

        emulator.AssertState(Pc: 0xc004);
    }

    [TestMethod]
    public async Task SwitchRomNotFireHigh()
    {
        var emulator = new Emulator();

        emulator.Breakpoints[0xc001] = 1;
        emulator.Breakpoints[0x210000 + (0x0000 * 0) + 0x3ff1] = 1;

        emulator.RomBank[0x3ff0] = 0xea;
        emulator.RomBank[0x3ff1] = 0xea;
        emulator.RomBank[0x3ff2] = 0xea;
        emulator.RomBank[0x3ff3] = 0xdb;

        emulator.RomBank[0x7ff0] = 0xea;
        emulator.RomBank[0x7ff1] = 0xea;
        emulator.RomBank[0x7ff2] = 0xea;
        emulator.RomBank[0x7ff3] = 0xdb;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #01
                sta $01

                jmp $fff0",
                emulator, expectedResult: Emulator.EmulatorResult.DebugOpCode);

        emulator.AssertState(Pc: 0xfff4);
    }

    [TestMethod]
    public async Task SwitchRomTwiceNotFire()
    {
        var emulator = new Emulator();

        emulator.Breakpoints[0x210000 + (0x0000 * 1) + 0x0001] = 1;

        emulator.RomBank[0x0000] = 0xea;
        emulator.RomBank[0x0001] = 0xea;
        emulator.RomBank[0x0002] = 0xea;
        emulator.RomBank[0x0003] = 0xdb;

        emulator.RomBank[0x4000] = 0xea;
        emulator.RomBank[0x4001] = 0xea;
        emulator.RomBank[0x4002] = 0xea;
        emulator.RomBank[0x4003] = 0xdb;

        emulator.RomBank[0x8000] = 0xea;
        emulator.RomBank[0x8001] = 0xea;
        emulator.RomBank[0x8002] = 0xea;
        emulator.RomBank[0x8003] = 0xdb;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #01
                sta $01

                lda #02
                sta $01

                jmp $c000",
                emulator, expectedResult: Emulator.EmulatorResult.DebugOpCode);

        emulator.AssertState(Pc: 0xc004);
    }

    [TestMethod]
    public async Task SwitchRomTwiceNotFireHigh()
    {
        var emulator = new Emulator();

        emulator.Breakpoints[0x210000 + (0x0000 * 1) + 0x3ff1] = 1;

        emulator.RomBank[0x3ff0] = 0xea;
        emulator.RomBank[0x3ff1] = 0xea;
        emulator.RomBank[0x3ff2] = 0xea;
        emulator.RomBank[0x3ff3] = 0xdb;

        emulator.RomBank[0x7ff0] = 0xea;
        emulator.RomBank[0x7ff1] = 0xea;
        emulator.RomBank[0x7ff2] = 0xea;
        emulator.RomBank[0x7ff3] = 0xdb;

        emulator.RomBank[0xbff0] = 0xea;
        emulator.RomBank[0xbff1] = 0xea;
        emulator.RomBank[0xbff2] = 0xea;
        emulator.RomBank[0xbff3] = 0xdb;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #01
                sta $01

                lda #02
                sta $01

                jmp $fff0",
                emulator, expectedResult: Emulator.EmulatorResult.DebugOpCode);

        emulator.AssertState(Pc: 0xfff4);
    }
}
