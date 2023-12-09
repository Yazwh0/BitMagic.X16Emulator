using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Debugging;

[TestClass]
public class RamBreakpoints
{
    [TestMethod]
    public async Task NormalRam()
    {
        var emulator = new Emulator();

        emulator.Breakpoints[0x811] = 1;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                nop
                nop ; breakpoint
                nop 
                stp",
                emulator, expectedResult: Emulator.EmulatorResult.Breakpoint);

        emulator.AssertState(Pc: 0x811);
    }

    [TestMethod]
    public async Task BankedRam()
    {
        var emulator = new Emulator();

        emulator.Breakpoints[0x10000 + 0x2000 + 0x811] = 1; // 0x811 in ram bank 1

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #1
                sta RAM_BANK
                
                lda #$ea ; nop
                sta $a810
                sta $a811
                sta $a812
                lda #$db
                sta $a813

                jmp $a810
                stp",
                emulator, expectedResult: Emulator.EmulatorResult.Breakpoint);

        emulator.AssertState(Pc: 0xa811);
    }

    [TestMethod]
    public async Task BankedRam2()
    {
        var emulator = new Emulator();

        emulator.Breakpoints[0x10000 + (0x2000 * 2) + 0x811] = 1; // 0x811 in ram bank 2

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #2
                sta RAM_BANK
                
                lda #$ea ; nop
                sta $a810
                sta $a811
                sta $a812
                lda #$db
                sta $a813

                jmp $a810
                stp",
                emulator, expectedResult: Emulator.EmulatorResult.Breakpoint);

        emulator.AssertState(Pc: 0xa811);
    }

    [TestMethod]
    public async Task BankedRom()
    {
        var emulator = new Emulator();

        emulator.Breakpoints[0x210000 + 0x4000 + 0x811] = 1; // 0x811 in rom bank 1
        emulator.RomBank[0x4000 + 0x810] = 0xea; // nops
        emulator.RomBank[0x4000 + 0x811] = 0xea;
        emulator.RomBank[0x4000 + 0x812] = 0xea;
        emulator.RomBank[0x4000 + 0x813] = 0xdb; // stp

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #1
                sta ROM_BANK
                
                jmp $c810
                stp",
                emulator, expectedResult: Emulator.EmulatorResult.Breakpoint);

        emulator.AssertState(Pc: 0xc811);
    }
}
