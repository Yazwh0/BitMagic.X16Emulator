using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Vera.Fx;

// 4 bit mode nibble addressing, following the VERA Verilog (addr_data.v). ADDRx_H bit 1 is the nibble address (0 is
// the high nibble, the left pixel) and bit 2 the nibble increment, which only applies in 4 bit mode with an
// increment of 0.
[TestClass]
public class Nibble
{
    private const int FxCtrl4Bit = 0b100;

    // Runs the code after setting FX_CTRL and ADDR0 (address $00100), then stops.
    private static async Task<Emulator> Run(int fxCtrl, int addrH, string code)
    {
        var emulator = new Emulator();
        emulator.Vera.Vram.Slice(0, 0x10000).Clear();

        await X16TestHelper.Emulate(@$"
                .machine CommanderX16R40
                .org $810
                stp
                lda #DC_SEL_2
                sta CTRL
                lda #{fxCtrl}
                sta FX_CTRL
                stz CTRL
                lda #$00
                sta ADDRx_L
                lda #$01
                sta ADDRx_M
                lda #{addrH}
                sta ADDRx_H
                {code}
                stp",
            emulator);

        emulator.EmulateWithTimeout();
        return emulator;
    }

    [TestMethod]
    public async Task AddrH_NibbleBits()
    {
        using var emulator = await Run(FxCtrl4Bit, 0b0000_0110, "");

        Assert.AreEqual(1, emulator.Vera.Data0_Nibble);
        Assert.AreEqual(1, emulator.Vera.Data0_NibbleIncrement);
        Assert.AreEqual(0b0000_0110, emulator.Memory[0x9f22], "Read back.");
    }

    [TestMethod]
    public async Task AddrH_NibbleDecrement()
    {
        using var emulator = await Run(FxCtrl4Bit, 0b0000_1100, "");

        Assert.AreEqual(0, emulator.Vera.Data0_Nibble);
        Assert.AreEqual(-1, emulator.Vera.Data0_NibbleIncrement);
        Assert.AreEqual(0b0000_1100, emulator.Memory[0x9f22], "Read back.");
    }

    // $100 n0 -> $100 n1 -> $101 n0 -> $101 n1
    [TestMethod]
    public async Task NibbleIncrement_Forward()
    {
        using var emulator = await Run(FxCtrl4Bit, 0b0000_0100, "lda DATA0\nlda DATA0\nlda DATA0");

        Assert.AreEqual(0x101, emulator.Vera.Data0_Address);
        Assert.AreEqual(1, emulator.Vera.Data0_Nibble);
        Assert.AreEqual(0x01, emulator.Memory[0x9f20]);
        Assert.AreEqual(0b0000_0110, emulator.Memory[0x9f22], "The nibble shows in ADDRx_H.");
    }

    // $100 n0 -> $0ff n1 -> $0ff n0 -> $0fe n1
    [TestMethod]
    public async Task NibbleIncrement_Backward()
    {
        using var emulator = await Run(FxCtrl4Bit, 0b0000_1100, "lda DATA0\nlda DATA0\nlda DATA0");

        Assert.AreEqual(0x0fe, emulator.Vera.Data0_Address);
        Assert.AreEqual(1, emulator.Vera.Data0_Nibble);
        Assert.AreEqual(0b0000_1110, emulator.Memory[0x9f22]);
    }

    [TestMethod]
    public async Task NibbleIncrement_Not4BitMode_DoesntStep()
    {
        using var emulator = await Run(0, 0b0000_0100, "lda DATA0\nlda DATA0\nlda DATA0");

        Assert.AreEqual(0x100, emulator.Vera.Data0_Address);
        Assert.AreEqual(0, emulator.Vera.Data0_Nibble);
    }

    [TestMethod]
    public async Task NibbleIncrement_WithAnIncrement_StepsBytes()
    {
        using var emulator = await Run(FxCtrl4Bit, 0b0001_0100, "lda DATA0\nlda DATA0\nlda DATA0");

        Assert.AreEqual(0x103, emulator.Vera.Data0_Address);
        Assert.AreEqual(0, emulator.Vera.Data0_Nibble);
    }

    [TestMethod]
    public async Task NibbleIncrement_TurningOn4BitMode()
    {
        // ADDR0 is set first, then 4 bit mode is turned on
        using var emulator = await Run(0, 0b0000_0100, @"
                lda #DC_SEL_2
                sta CTRL
                lda #4
                sta FX_CTRL
                stz CTRL
                lda DATA0
                lda DATA0");

        Assert.AreEqual(0x101, emulator.Vera.Data0_Address);
        Assert.AreEqual(0, emulator.Vera.Data0_Nibble);
    }

    [TestMethod]
    [DataRow(0, 0x2b)]  // high nibble from the high nibble of the value
    [DataRow(1, 0xa3)]  // low nibble from the low nibble
    public async Task Write_4BitMode_OnlyTheAddressedNibble(int nibble, int expected)
    {
        using var emulator = new Emulator();
        emulator.Vera.Vram[0x100] = 0xab;

        await X16TestHelper.Emulate(@$"
                .machine CommanderX16R40
                .org $810
                stp
                lda #DC_SEL_2
                sta CTRL
                lda #4
                sta FX_CTRL
                stz CTRL
                lda #$00
                sta ADDRx_L
                lda #$01
                sta ADDRx_M
                lda #{nibble << 1}
                sta ADDRx_H
                lda #$23
                sta DATA0
                stp",
            emulator);

        emulator.EmulateWithTimeout();

        Assert.AreEqual(expected, emulator.Vera.Vram[0x100]);
    }

    [TestMethod]
    public async Task Write_8BitMode_WholeByte()
    {
        using var emulator = new Emulator();
        emulator.Vera.Vram[0x100] = 0xab;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp
                lda #$00
                sta ADDRx_L
                lda #$01
                sta ADDRx_M
                lda #%00000010
                sta ADDRx_H
                lda #$23
                sta DATA0
                stp",
            emulator);

        emulator.EmulateWithTimeout();

        Assert.AreEqual(0x23, emulator.Vera.Vram[0x100]);
    }

    // Line draw on a 4bpp bitmap, 160 bytes a row. Draws pixels of colour $f from (x, y) with an X increment of 3/8.
    private static async Task<Emulator> DrawLine(int x, int y, int addr1H, int addr0H, int pixels, bool fixedLineDraw = false)
    {
        var emulator = new Emulator();
        emulator.Vera.Vram.Slice(0, 0x10000).Clear();
        emulator.FixedFxLineDraw = fixedLineDraw;

        var address = y * 160 + x / 2;
        addr1H |= (x & 1) << 1;

        await X16TestHelper.Emulate(@$"
                .machine CommanderX16R40
                .org $810
                stp
                lda #DC_SEL_2
                sta CTRL
                lda #{addr0H}
                sta ADDRx_H
                lda #%00000101      ; 4 bit mode, line draw
                sta FX_CTRL
                lda #DC_SEL_2 + 1
                sta CTRL
                lda #{address & 0xff}
                sta ADDRx_L
                lda #{address >> 8}
                sta ADDRx_M
                lda #{addr1H}
                sta ADDRx_H
                lda #DC_SEL_3
                sta CTRL
                lda #$c0
                sta FX_X_INCR_L
                lda #$00
                sta FX_X_INCR_H
                lda #$ff
                ldx #{pixels}
            .loop:
                sta DATA1
                dex
                bne loop
                stp",
            emulator);

        emulator.EmulateWithTimeout();
        return emulator;
    }

    // the minor axis position of pixel k, starting half way through a pixel and adding 3/8 after each one
    private static int MinorAt(int k) => (256 + k * 0xc0) >> 9;

    private static void AssertPixels(Emulator emulator, IEnumerable<(int X, int Y)> pixels)
    {
        var expected = new byte[0x10000];
        foreach (var (x, y) in pixels)
            expected[y * 160 + x / 2] |= (byte)((x & 1) == 0 ? 0xf0 : 0x0f);

        var actual = emulator.Vera.Vram.Slice(0, 0x10000).ToArray();

        for (var i = 0; i < expected.Length; i++)
            if (expected[i] != actual[i])
                Assert.Fail($"VRAM ${i:X5} is ${actual[i]:X2}, expected ${expected[i]:X2} (x {(i % 160) * 2}, y {i / 160}).");
    }

    // fixedFxLineDraw only changes steep lines
    [TestMethod]
    [DataRow(1, 1, false)]
    [DataRow(-1, 1, false)]
    [DataRow(1, -1, false)]
    [DataRow(-1, -1, false)]
    [DataRow(1, 1, true)]
    [DataRow(-1, 1, true)]
    [DataRow(1, -1, true)]
    [DataRow(-1, -1, true)]
    public async Task LineDraw_4bpp_Shallow(int sx, int sy, bool fixedLineDraw)
    {
        const int pixels = 40;
        var addr1H = sx > 0 ? 0b0000_0100 : 0b0000_1100;  // half a byte
        var addr0H = sy > 0 ? 0b1101_0000 : 0b1101_1000;  // 160 bytes, a row

        using var emulator = await DrawLine(81, 60, addr1H, addr0H, pixels, fixedLineDraw);

        AssertPixels(emulator, Enumerable.Range(0, pixels).Select(k => (81 + sx * k, 60 + sy * MinorAt(k))));
        // ADDR1 is left on the next pixel
        var nextX = 81 + sx * pixels;
        var nextY = 60 + sy * MinorAt(pixels);
        Assert.AreEqual(nextY * 160 + nextX / 2, emulator.Vera.Data1_Address);
        Assert.AreEqual(nextX & 1, emulator.Vera.Data1_Nibble);
    }

    // ADDR0's nibble increment adds what ADDR0 would step by to ADDR1's address, but doesn't change ADDR1's nibble.
    // Forwards that's a byte from nibble 1 and nothing from nibble 0, so a steep 4bpp line moves 2 pixels or not at all.
    [TestMethod]
    [DataRow(1, 2)]
    [DataRow(0, 0)]
    public async Task LineDraw_4bpp_Steep_Addr0Nibble(int addr0Nibble, int pixelsPerStep)
    {
        const int pixels = 40;

        using var emulator = await DrawLine(80, 60, 0b1101_0000, 0b0000_0100 | (addr0Nibble << 1), pixels);

        AssertPixels(emulator, Enumerable.Range(0, pixels).Select(k => (80 + pixelsPerStep * MinorAt(k), 60 + k)));
    }

    // Going backwards from nibble 0, the hardware steps ADDR1 back a byte, two pixels
    [TestMethod]
    public async Task LineDraw_4bpp_Steep_Backwards_Hardware()
    {
        const int pixels = 40;

        using var emulator = await DrawLine(80, 60, 0b1101_0000, 0b0000_1100, pixels);

        AssertPixels(emulator, Enumerable.Range(0, pixels).Select(k => (80 - 2 * MinorAt(k), 60 + k)));
    }

    // With fixedFxLineDraw, ADDR1's nibble steps in ADDR0's direction so steep lines draw properly, from either nibble
    [TestMethod]
    [DataRow(1, 1, 80)]
    [DataRow(-1, 1, 80)]
    [DataRow(1, -1, 80)]
    [DataRow(-1, -1, 80)]
    [DataRow(1, 1, 81)]
    [DataRow(-1, -1, 81)]
    public async Task LineDraw_4bpp_Steep_Fixed(int sx, int sy, int x)
    {
        const int pixels = 40;
        var addr1H = sy > 0 ? 0b1101_0000 : 0b1101_1000;  // 160 bytes, a row
        var addr0H = sx > 0 ? 0b0000_0100 : 0b0000_1100;  // half a byte

        using var emulator = await DrawLine(x, 60, addr1H, addr0H, pixels, fixedLineDraw: true);

        AssertPixels(emulator, Enumerable.Range(0, pixels).Select(k => (x + sx * MinorAt(k), 60 + sy * k)));
    }

    [TestMethod]
    public void FixedFxLineDraw_KeptOverRestore()
    {
        using var emulator = new Emulator();

        var saved = emulator.SaveState();
        emulator.FixedFxLineDraw = true;
        emulator.RestoreState(saved);

        Assert.IsTrue(emulator.FixedFxLineDraw, "It's config, not machine state.");
    }
}
