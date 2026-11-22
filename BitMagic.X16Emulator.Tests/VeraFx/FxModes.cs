using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Vera.Fx;

// The rest of VERA FX, following the VERA Verilog (addr_data.v, vram_if.v, top.v): the Y/X2 increment and the
// positions, the polygon filler, the affine helper, 2-bit polygon poke, one-byte cache cycling, 16-bit hop,
// transparent writes and the 4-bit cache index. Registers are written by address, $9F29 to $9F2C, as their meaning
// depends on DCSEL.
[TestClass]
public class FxModes
{
    private static Emulator NewEmulator()
    {
        var emulator = new Emulator();
        emulator.Vera.Vram.Slice(0, 0x1f9c0).Clear();
        return emulator;
    }

    // Runs the code between two stp's
    private static async Task<Emulator> Run(string code, Action<Emulator>? setup = null)
    {
        var emulator = NewEmulator();
        setup?.Invoke(emulator);

        await X16TestHelper.Emulate($@"
                .machine CommanderX16R40
                .org $810
                stp
                {code}
                stp",
            emulator);

        emulator.EmulateWithTimeout();
        return emulator;
    }

    // ---- increments and positions ----

    [TestMethod]
    public async Task Increments_SignedWithX32()
    {
        using var emulator = await Run(@"
                lda #DC_SEL_3
                sta CTRL
                lda #$80
                sta $9f29       ; X incr L
                lda #$81        ; X incr H, x32
                sta $9f2a
                lda #$80
                sta $9f2b       ; Y incr L
                lda #$01
                sta $9f2c       ; Y incr H");

        Assert.AreEqual(0x00180000u, emulator.VeraFx.IncrementX, "$180 / $200 x 32, as 16.16");
        Assert.IsTrue(emulator.VeraFx.Mult32X);
        Assert.AreEqual(0x0000c000u, emulator.VeraFx.IncrementY, "$180 / $200, as 16.16");
        Assert.IsFalse(emulator.VeraFx.Mult32Y);
    }

    [TestMethod]
    public async Task Increment_Negative()
    {
        using var emulator = await Run(@"
                lda #DC_SEL_3
                sta CTRL
                stz $9f2b
                lda #$7f        ; -$100 / $200
                sta $9f2c");

        Assert.AreEqual(0xffff8000u, emulator.VeraFx.IncrementY);
    }

    [TestMethod]
    public async Task Positions()
    {
        using var emulator = await Run(@"
                lda #DC_SEL_4
                sta CTRL
                lda #$34
                sta $9f29       ; X L
                lda #$85        ; X H, the -9 bit and bits 10:8
                sta $9f2a
                lda #$21
                sta $9f2b       ; Y L
                lda #$02
                sta $9f2c       ; Y H
                lda #DC_SEL_5
                sta CTRL
                lda #$40
                sta $9f29       ; X sub pixel");

        Assert.AreEqual(0x05344080u, emulator.VeraFx.PositionX, "$534, sub pixel $40 and the -9 bit");
        Assert.AreEqual(0x02218000u, emulator.VeraFx.PositionY, "$221, still half a pixel");
    }

    [TestMethod]
    public async Task YIncrementHigh_PolygonMode_ResetsSubPixel()
    {
        using var emulator = await Run(@"
                lda #DC_SEL_2
                sta CTRL
                lda #2          ; polygon filler
                sta FX_CTRL
                lda #DC_SEL_3
                sta CTRL
                stz $9f2c",
            e => e.VeraFx.PositionY = 0x00123456);

        Assert.AreEqual(0x00128000u, emulator.VeraFx.PositionY);
    }

    // ---- polygon filler ----

    // ADDR0 is the start of the row, stepping a row (320). X1 starts at 100 and moves -0.5 each step, X2 starts at 110
    // and moves +0.5. Writing a position in polygon mode sets ADDR1 = ADDR0 + X1.
    private const string PolygonSetup = @"
                lda #DC_SEL_2
                sta CTRL
                lda #2          ; polygon filler
                sta FX_CTRL
                stz CTRL
                lda #$e0        ; ADDR0 = 0, increment 320
                sta ADDRx_H
                stz ADDRx_M
                stz ADDRx_L
                lda #DC_SEL_3
                sta CTRL
                stz $9f29
                lda #$7f        ; X1 increment -0.5
                sta $9f2a
                stz $9f2b
                lda #$01        ; X2 increment +0.5
                sta $9f2c
                lda #DC_SEL_4
                sta CTRL
                lda #100
                sta $9f29
                stz $9f2a
                lda #110
                sta $9f2b
                stz $9f2c";

    [TestMethod]
    public async Task Polygon_PositionWrite_SetsAddr1()
    {
        using var emulator = await Run(PolygonSetup);

        Assert.AreEqual(100, emulator.Vera.Data1_Address);
        Assert.AreEqual(0x00648000u, emulator.VeraFx.PositionX, "100.5");
        Assert.AreEqual(0x006e8000u, emulator.VeraFx.PositionY, "110.5");
    }

    [TestMethod]
    public async Task Polygon_Fill()
    {
        using var emulator = NewEmulator();

        await X16TestHelper.Emulate($@"
                .machine CommanderX16R40
                .org $810
                stp
                {PolygonSetup}
                lda DATA1       ; X1 100, X2 111, ADDR1 = 0 + 100
                lda #DC_SEL_5
                sta CTRL
                ldx $9f2b       ; fill length 11
                ldy $9f2c
                stp
                lda DATA0       ; X1 99.5, X2 111.5, ADDR0 = 320
                lda DATA1       ; X1 99, X2 112, ADDR1 = 320 + 99
                ldx $9f2b       ; fill length 13
                ldy $9f2c
                stp",
            emulator);

        emulator.EmulateWithTimeout();

        Assert.AreEqual(100, emulator.Vera.Data1_Address);
        Assert.AreEqual(0x00640000u, emulator.VeraFx.PositionX);
        Assert.AreEqual(0x006f0000u, emulator.VeraFx.PositionY);
        Assert.AreEqual(0x16, emulator.X, "11: fill length 3 in bit 4, 2:0 in 3:1, X1 bits 1:0 (00) in 6:5");
        Assert.AreEqual(0x02, emulator.Y, "fill length 9:3 in 7:1");

        emulator.EmulateWithTimeout();

        Assert.AreEqual(320, emulator.Vera.Data0_Address);
        Assert.AreEqual(320 + 99, emulator.Vera.Data1_Address);
        Assert.AreEqual(0x00630000u, emulator.VeraFx.PositionX);
        Assert.AreEqual(0x00700000u, emulator.VeraFx.PositionY);
        Assert.AreEqual(0x7a, emulator.X, "13, X1 bits 1:0 are 11");
        Assert.AreEqual(0x02, emulator.Y);
    }

    // in 4-bit mode ADDR1 = ADDR0 + X1 / 2, and the nibble is X1's bit 0. X1 is signed.
    [TestMethod]
    [DataRow(101, 0x1000 + 50, 1)]
    [DataRow(100, 0x1000 + 50, 0)]
    [DataRow(0x7ff, 0x1000 - 1, 1)]   // -1
    public async Task Polygon_4Bit_Addr1(int x, int address, int nibble)
    {
        using var emulator = await Run($@"
                lda #DC_SEL_2
                sta CTRL
                lda #6          ; 4-bit, polygon filler
                sta FX_CTRL
                stz CTRL
                lda #$10
                sta ADDRx_M
                lda #DC_SEL_4
                sta CTRL
                lda #{x & 0xff}
                sta $9f29
                lda #{x >> 8}
                sta $9f2a");

        Assert.AreEqual(address, emulator.Vera.Data1_Address);
        Assert.AreEqual(nibble, emulator.Vera.Data1_Nibble);
    }

    [TestMethod]
    public async Task Polygon_8Bit_NegativeX1()
    {
        using var emulator = await Run(@"
                lda #DC_SEL_2
                sta CTRL
                lda #2
                sta FX_CTRL
                stz CTRL
                lda #$10
                sta ADDRx_M
                lda #DC_SEL_4
                sta CTRL
                lda #$fe        ; -2
                sta $9f29
                lda #$07
                sta $9f2a");

        Assert.AreEqual(0x1000 - 2, emulator.Vera.Data1_Address);
    }

    // FX_POLY_FILL_L/H follow the positions, without needing a DATA1 read
    [TestMethod]
    [DataRow(6, 5, 14, 0xb2, 0x02)]         // 4-bit, 9: 8 or more (bit 7), X1 bit 2 in bit 4, X1 1:0 (01) in 6:5, 9 & 7 in 3:1
    [DataRow(2, 5, 14, 0x32, 0x02)]         // 8-bit, 9: fill length 3 in bit 4, X1 1:0 in 6:5
    [DataRow(2, 0, 20, 0x88, 0x04)]         // 8-bit, 20: 16 or more
    [DataRow(2, 20, 10, 0x80, 0xfc)]        // negative: only bit 7 in the low byte, the high byte is the raw 10 bits
    public async Task Polygon_FillLength(int fxCtrl, int x1, int x2, int low, int high)
    {
        using var emulator = await Run($@"
                lda #DC_SEL_2
                sta CTRL
                lda #{fxCtrl}
                sta FX_CTRL
                lda #DC_SEL_4
                sta CTRL
                lda #{x1}
                sta $9f29
                stz $9f2a
                lda #{x2}
                sta $9f2b
                stz $9f2c
                lda #DC_SEL_5
                sta CTRL
                ldx $9f2b
                ldy $9f2c");

        Assert.AreEqual(low, emulator.X, "FX_POLY_FILL_L");
        Assert.AreEqual(high, emulator.Y, "FX_POLY_FILL_H");
    }

    // ---- affine helper ----

    // An 8x8 map at $12000 where each cell holds its own number, 8bpp tiles at $10000 where tile k's byte j is k + j.
    // So the pixel at (x, y) is tile (y / 8) * 8 + x / 8, plus (y % 8) * 8 + x % 8.
    private static void AffineData(Emulator emulator)
    {
        var vram = emulator.Vera.Vram;
        for (var i = 0; i < 64; i++)
            vram[0x12000 + i] = (byte)i;
        for (var k = 0; k < 64; k++)
            for (var j = 0; j < 64; j++)
                vram[0x10000 + k * 64 + j] = (byte)(k + j);
    }

    private static string AffineSetup(int x, int y, int fxCtrl = 3, int tileBase = 0x80) => $@"
                lda #DC_SEL_2
                sta CTRL
                lda #{fxCtrl}
                sta FX_CTRL
                lda #{tileBase}  ; tile base $10000, clip in bit 1
                sta $9f2a
                lda #$91        ; map base $12000, 8x8
                sta $9f2b
                lda #DC_SEL_3
                sta CTRL
                stz $9f29
                lda #$02        ; X increment 1
                sta $9f2a
                stz $9f2b
                stz $9f2c
                lda #DC_SEL_4
                sta CTRL
                lda #{x & 0xff}
                sta $9f29
                lda #{x >> 8}
                sta $9f2a
                lda #{y & 0xff}
                sta $9f2b
                lda #{y >> 8}
                sta $9f2c";

    [TestMethod]
    public async Task Affine_Read()
    {
        using var emulator = await Run(AffineSetup(10, 20) + @"
                lda DATA1       ; (10, 20)
                ldx DATA1       ; (11, 20)
                ldy DATA1       ; (12, 20)", AffineData);

        // tile 17, (2, 4) in the tile
        Assert.AreEqual(17 + 34, emulator.A);
        Assert.AreEqual(17 + 35, emulator.X);
        Assert.AreEqual(17 + 36, emulator.Y);
        Assert.AreEqual(0x10000 + 17 * 64 + 37, emulator.Vera.Data1_Address, "on to (13, 20)");
    }

    [TestMethod]
    [DataRow(0x80, 16 + 38)]   // wraps to tile 16
    [DataRow(0x82, 38)]        // clip, tile 0
    public async Task Affine_OutsideTheMap(int tileBase, int expected)
    {
        using var emulator = await Run(AffineSetup(70, 20, tileBase: tileBase) + @"
                lda DATA1", AffineData);

        Assert.AreEqual(expected, emulator.A);
    }

    [TestMethod]
    public async Task Affine_4Bit_Address()
    {
        using var emulator = await Run(AffineSetup(10, 20, fxCtrl: 7), AffineData);

        // tile 17 at 32 bytes a tile, (2, 4) in the tile is 4 * 4 + 2 / 2, nibble 0
        Assert.AreEqual(0x10000 + 17 * 32 + 17, emulator.Vera.Data1_Address);
        Assert.AreEqual(0, emulator.Vera.Data1_Nibble);
    }

    [TestMethod]
    public async Task Affine_Write()
    {
        using var emulator = await Run(AffineSetup(10, 20) + @"
                lda #$ee
                sta DATA1       ; writes (10, 20), then on to (11, 20)", AffineData);

        Assert.AreEqual(0xee, emulator.Vera.Vram[0x10000 + 17 * 64 + 34]);
        Assert.AreEqual(0x10000 + 17 * 64 + 35, emulator.Vera.Data1_Address);
    }

    // ---- 2-bit polygon poke ----

    [TestMethod]
    public async Task TwoBitPoke()
    {
        using var emulator = await Run(@"
                lda #DC_SEL_2
                sta CTRL
                lda #6          ; 4-bit, polygon filler
                sta FX_CTRL
                lda #1          ; 2-bit polygon
                sta $9f2a
                lda #DC_SEL_2 + 1
                sta CTRL
                lda #$50
                sta ADDRx_M
                lda #$fe        ; only the low 2 bits are used
                sta ADDRx_L
                lda #$40        ; bits 7:6 = 01: bits 5:4
                sta DATA1",
            e =>
            {
                e.VeraFx.Cache = 0xe4;          // %11 10 01 00
                e.VeraFx.CacheIndex = 0;
                e.Vera.Vram[0x5002] = 0xff;
            });

        Assert.AreEqual(0xef, emulator.Vera.Vram[0x5002], "bits 5:4 from the cache (10)");
        Assert.AreEqual(0x5002, emulator.Vera.Data1_Address, "doesn't move");
        Assert.IsFalse(emulator.VeraFx.TwoBitPoke);
    }

    // ---- one-byte cache cycling ----

    [TestMethod]
    public async Task OneByteCycling_Write()
    {
        using var emulator = await Run(@"
                lda #DC_SEL_2
                sta CTRL
                lda #$10        ; one-byte cycling
                sta FX_CTRL
                stz CTRL
                lda #$01
                sta ADDRx_M
                lda #$ff
                sta DATA0",
            e =>
            {
                e.VeraFx.Cache = 0x44332211;
                e.VeraFx.CacheIndex = 4;        // byte 2
            });

        Assert.AreEqual(0x33, emulator.Vera.Vram[0x100]);
    }

    [TestMethod]
    [DataRow(0x00, new byte[] { 0x33, 0x33, 0x33, 0x33 })]
    [DataRow(0x0f, new byte[] { 0xaa, 0xaa, 0x33, 0x33 })]  // the mask stops bytes 0 and 1
    public async Task OneByteCycling_CacheWrite(int mask, byte[] expected)
    {
        using var emulator = await Run($@"
                lda #DC_SEL_2
                sta CTRL
                lda #$50        ; cache write, one-byte cycling
                sta FX_CTRL
                stz CTRL
                lda #$01
                sta ADDRx_M
                lda #{mask}
                sta DATA0",
            e =>
            {
                e.VeraFx.Cache = 0x44332211;
                e.VeraFx.CacheIndex = 4;
                new byte[] { 0xaa, 0xaa, 0xaa, 0xaa }.CopyTo(e.Vera.Vram.Slice(0x100));
            });

        CollectionAssert.AreEqual(expected, emulator.Vera.Vram.Slice(0x100, 4).ToArray());
    }

    [TestMethod]
    [DataRow(false, 2)]
    [DataRow(true, 2)]
    public async Task OneByteCycling_PolygonDataRead_MovesTheCache(bool twoByte, int after)
    {
        using var emulator = await Run(@"
                lda #DC_SEL_2
                sta CTRL
                lda #$12        ; one-byte cycling, polygon filler
                sta FX_CTRL
                lda DATA0",
            e =>
            {
                e.VeraFx.CacheIndex = 0;
                e.VeraFx.TwoByteCacheIncr = twoByte;
            });

        Assert.AreEqual(after, emulator.VeraFx.CacheIndex);
    }

    [TestMethod]
    public async Task OneByteCycling_PolygonDataRead_TwoByte_Toggles()
    {
        using var emulator = await Run(@"
                lda #DC_SEL_2
                sta CTRL
                lda #$12
                sta FX_CTRL
                lda DATA0
                lda DATA0",
            e =>
            {
                e.VeraFx.CacheIndex = 0;
                e.VeraFx.TwoByteCacheIncr = true;
            });

        Assert.AreEqual(0, emulator.VeraFx.CacheIndex, "byte 0, 1, 0");
    }

    // fxFixed ignores two-byte mode here, as the official emulator does
    [TestMethod]
    public async Task OneByteCycling_PolygonDataRead_TwoByte_FxFixed_MovesOn()
    {
        using var emulator = await Run(@"
                lda #DC_SEL_2
                sta CTRL
                lda #$12
                sta FX_CTRL
                lda DATA0
                lda DATA0",
            e =>
            {
                e.FxFixed = true;
                e.VeraFx.CacheIndex = 0;
                e.VeraFx.TwoByteCacheIncr = true;
            });

        Assert.AreEqual(4, emulator.VeraFx.CacheIndex, "byte 0, 1, 2");
    }

    // ---- 16-bit hop ----

    [TestMethod]
    [DataRow(0x30, 0x01, new[] { 0x102, 0x105, 0x106, 0x109 })]   // 4: +1 from the start alignment, then +3
    [DataRow(0xe0, 0x00, new[] { 0x101, 0x240, 0x241, 0x380 })]   // 320: +1 then +319
    [DataRow(0x20, 0x00, new[] { 0x102, 0x104, 0x106, 0x108 })]   // 2: no hop
    public async Task SixteenBitHop(int addrH, int addrL, int[] addresses)
    {
        using var emulator = NewEmulator();

        await X16TestHelper.Emulate($@"
                .machine CommanderX16R40
                .org $810
                stp
                lda #DC_SEL_2
                sta CTRL
                lda #$08        ; 16-bit hop
                sta FX_CTRL
                lda #DC_SEL_2 + 1
                sta CTRL
                lda #{addrH}
                sta ADDRx_H
                lda #$01
                sta ADDRx_M
                lda #{addrL}
                sta ADDRx_L
                stp
                lda DATA1
                stp
                lda DATA1
                stp
                lda DATA1
                stp
                lda DATA1
                stp",
            emulator);

        emulator.EmulateWithTimeout();      // the setup

        foreach (var address in addresses)
        {
            emulator.EmulateWithTimeout();  // a read
            Assert.AreEqual(address, emulator.Vera.Data1_Address);
        }
    }

    // ---- transparent writes ----

    [TestMethod]
    [DataRow(0x80, 0x00, 0, 0xab)]  // 8-bit, 0 isn't written
    [DataRow(0x80, 0x05, 0, 0x05)]
    [DataRow(0x84, 0x0f, 0, 0xab)]  // 4-bit nibble 0, its nibble of the value is 0
    [DataRow(0x84, 0x3f, 0, 0x3b)]
    [DataRow(0x84, 0xf0, 1, 0xab)]  // 4-bit nibble 1
    [DataRow(0x84, 0xf7, 1, 0xa7)]
    public async Task TransparentWrites(int fxCtrl, int value, int nibble, int expected)
    {
        using var emulator = await Run($@"
                lda #DC_SEL_2
                sta CTRL
                lda #{fxCtrl}
                sta FX_CTRL
                stz CTRL
                lda #$01
                sta ADDRx_M
                lda #{nibble << 1}
                sta ADDRx_H
                lda #{value}
                sta DATA0",
            e => e.Vera.Vram[0x100] = 0xab);

        Assert.AreEqual(expected, emulator.Vera.Vram[0x100]);
    }

    // transparency, nibbles and one-byte cycling only apply to main VRAM, the palette gets the whole byte
    [TestMethod]
    public async Task FxWrites_NotForThePalette()
    {
        using var emulator = await Run(@"
                lda #DC_SEL_2
                sta CTRL
                lda #$94        ; transparent, one-byte cycling, 4-bit
                sta FX_CTRL
                stz CTRL
                lda #$01
                sta ADDRx_H
                lda #$fa
                sta ADDRx_M
                stz ADDRx_L
                lda #$12
                sta DATA0",
            e => e.VeraFx.Cache = 0);

        Assert.AreEqual(0x12, emulator.Vera.Vram[0x1fa00]);
    }

    // ---- FX_CTRL and the cache index ----

    [TestMethod]
    public async Task FxCtrl_ReadsBack_With16BitHop()
    {
        using var emulator = await Run(@"
                lda #DC_SEL_2
                sta CTRL
                lda #$0a        ; 16-bit hop, polygon filler
                sta FX_CTRL
                stz CTRL
                lda #DC_SEL_2
                sta CTRL
                lda FX_CTRL");

        Assert.AreEqual(0x0a, emulator.A);
    }

    // 4-bit cache fill: index 3 is byte 1's low nibble, then byte 2's high nibble, then its low nibble
    [TestMethod]
    public async Task CacheFill_4Bit_FromTheIndex()
    {
        using var emulator = await Run(@"
                lda #DC_SEL_2
                sta CTRL
                lda #$24        ; cache fill, 4-bit
                sta FX_CTRL
                lda #%00000110  ; byte 1, nibble 1
                sta FX_MULT
                stz CTRL
                lda #%00000100  ; nibble increment
                sta ADDRx_H
                stz ADDRx_M
                stz ADDRx_L
                lda DATA0       ; 1
                lda DATA0       ; 2
                lda DATA0       ; 3",
            e =>
            {
                e.VeraFx.Cache = 0;
                e.Vera.Vram[0] = 0x12;
                e.Vera.Vram[1] = 0x34;
            });

        Assert.AreEqual(0x00230100u, emulator.VeraFx.Cache);
        Assert.AreEqual(6, emulator.VeraFx.CacheIndex);
    }
}
