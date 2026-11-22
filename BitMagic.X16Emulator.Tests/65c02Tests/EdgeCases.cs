using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

// 65C02 behaviour from 6502.org's 65C02 opcodes and decimal mode tutorials, that isn't covered by the per opcode tests.
[TestClass]
public class EdgeCases
{
    private static async Task Run(Emulator emulator, string code) =>
        await X16TestHelper.Emulate($@"
                .machine CommanderX16R40
                .org $810
                {code}
                stp",
                emulator);

    // Z is from A AND the value before the write, so an earlier Z is cleared
    [TestMethod]
    public async Task Tsb_ClearsZero()
    {
        using var emulator = new Emulator();
        emulator.A = 0x03;
        emulator.Memory[0x10] = 0x01;
        emulator.Zero = true;

        await Run(emulator, "tsb $10");

        Assert.AreEqual(0x03, emulator.Memory[0x10]);
        emulator.AssertFlags(Zero: false);
    }

    [TestMethod]
    public async Task Trb_ClearsZero()
    {
        using var emulator = new Emulator();
        emulator.A = 0x01;
        emulator.Memory[0x1234] = 0x03;
        emulator.Zero = true;

        await Run(emulator, "trb $1234");

        Assert.AreEqual(0x02, emulator.Memory[0x1234]);
        emulator.AssertFlags(Zero: false);
    }

    [TestMethod]
    public async Task Adc_Decimal_Overflow()
    {
        using var emulator = new Emulator();
        emulator.A = 0x79;
        emulator.Carry = true;
        emulator.Decimal = true;

        await Run(emulator, "adc #$00");

        emulator.AssertState(A: 0x80, Clock: 3);
        emulator.AssertFlags(Negative: true, Overflow: true, Decimal: true);
    }

    [TestMethod]
    public async Task Sbc_Decimal_Overflow()
    {
        using var emulator = new Emulator();
        emulator.A = 0x80;
        emulator.Carry = true;
        emulator.Decimal = true;

        await Run(emulator, "sbc #$01");

        emulator.AssertState(A: 0x79, Clock: 3);
        emulator.AssertFlags(Overflow: true, Carry: true, Decimal: true);
    }

    // not valid bcd, the 65c02 gives a different result to the 6502
    [TestMethod]
    public async Task Sbc_Decimal_InvalidBcd()
    {
        using var emulator = new Emulator();
        emulator.A = 0x00;
        emulator.Decimal = true;

        await Run(emulator, "sbc #$0a");

        emulator.AssertState(A: 0x8f);
        emulator.AssertFlags(Negative: true, Decimal: true);
    }

    [TestMethod]
    public async Task Sbc_Decimal_Zp()
    {
        using var emulator = new Emulator();
        emulator.A = 0x42;
        emulator.Carry = true;
        emulator.Decimal = true;
        emulator.Memory[0x10] = 0x13;

        await Run(emulator, "sbc $10");

        emulator.AssertState(A: 0x29, Clock: 4);
        emulator.AssertFlags(Carry: true, Decimal: true);
    }

    [TestMethod]
    public async Task Nmi_ClearsDecimal()
    {
        using var emulator = new Emulator();
        emulator.Nmi = true;
        emulator.Decimal = true;

        emulator.RomBank[0x3ffa] = 0x00;
        emulator.RomBank[0x3ffb] = 0x09;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                nop
                stp
                .org $900
                stp",
                emulator);

        emulator.AssertState(Pc: 0x901);
        emulator.AssertFlags(InterruptDisable: true, Nmi: true);
        Assert.AreEqual(0b00101000, emulator.Memory[0x1fb] & 0b00111000, "pushed with D set and B clear");
    }

    // rom isn't written, but the flags are as if it was
    [TestMethod]
    public async Task Inc_Rom_SetsFlags()
    {
        using var emulator = new Emulator();
        emulator.RomBank[0x0000] = 0xff;

        await Run(emulator, "inc $c000");

        Assert.AreEqual(0xff, emulator.RomBank[0x0000]);
        emulator.AssertFlags(Zero: true);
    }

    [TestMethod]
    public async Task Dec_Rom_SetsFlags()
    {
        using var emulator = new Emulator();
        emulator.RomBank[0x0000] = 0x00;

        await Run(emulator, "dec $c000");

        Assert.AreEqual(0x00, emulator.RomBank[0x0000]);
        emulator.AssertFlags(Negative: true);
    }

    [TestMethod]
    public async Task Rol_Rom_CarryIn()
    {
        using var emulator = new Emulator();
        emulator.RomBank[0x0000] = 0x80;
        emulator.Carry = true;

        await Run(emulator, "rol $c000");

        Assert.AreEqual(0x80, emulator.RomBank[0x0000]);
        emulator.AssertFlags(Carry: true);
    }

    [TestMethod]
    public async Task Ror_Rom_CarryIn()
    {
        using var emulator = new Emulator();
        emulator.RomBank[0x0000] = 0x01;
        emulator.Carry = true;

        await Run(emulator, "ror $c000");

        Assert.AreEqual(0x01, emulator.RomBank[0x0000]);
        emulator.AssertFlags(Negative: true, Carry: true);
    }

    // $00 is the ram bank, so changing it any way switches bank
    [TestMethod]
    [DataRow("lda #$01\n sta $00\n asl $00")]
    [DataRow("lda #$04\n sta $00\n lsr $00")]
    [DataRow("lda #$01\n sta $00\n rol $00")]
    [DataRow("lda #$05\n sta $00\n ror $00")]
    [DataRow("lda #$02\n tsb $00")]
    [DataRow("lda #$03\n sta $00\n lda #$01\n trb $00")]
    [DataRow("smb1 $00")]
    [DataRow("lda #$03\n sta $00\n rmb0 $00")]
    public async Task ZpReadModifyWrite_SwitchesRamBank(string code)
    {
        using var emulator = new Emulator();
        emulator.RamBank[0x2000 * 2] = 0xff;

        await Run(emulator, $"{code}\n ldx $a000");

        Assert.AreEqual(0x02, emulator.Memory[0x00]);
        emulator.AssertState(X: 0xff);
    }

    [TestMethod]
    [DataRow("bit $12ff, x", 5)]
    [DataRow("asl $12ff, x", 7)]
    [DataRow("lsr $12ff, x", 7)]
    [DataRow("rol $12ff, x", 7)]
    [DataRow("ror $12ff, x", 7)]
    [DataRow("inc $12ff, x", 7)]
    [DataRow("inc $1200, x", 7)]
    public async Task AbsX_PageCross_Cycles(string code, int cycles)
    {
        using var emulator = new Emulator();
        emulator.X = 0x01;

        await Run(emulator, code);

        emulator.AssertState(Clock: (ulong)cycles);
    }

    // a read of $9f28 is the live scanline, which cmp has to see. Retries in case the line changes between the two.
    [TestMethod]
    public async Task Cmp_Scanline()
    {
        using var emulator = new Emulator();

        await Run(emulator, @"
                ldx #$00
            .loop:
                dex
                bne loop
            .retry:
                lda $9f28
                cmp $9f28
                bne retry");

        Assert.AreNotEqual(0x00, emulator.A, "should be past the first scanline");
        Assert.IsTrue(emulator.Zero);
    }
}
