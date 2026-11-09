using BitMagic.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Vera.Display;

// DC_VIDEO's output mode (bits 0-1). 0 is video off: VERA drives its outputs to 0, so the screen is black, but the
// timing, interrupts and rendering all carry on (vera-module top.v), issue #196. 1 to 3 (VGA, NTSC, RGB) show the
// picture; the emulator draws them all the same way.
[TestClass]
public class VideoOutput
{
    private const int Width = 800;
    private const int Height = 525;

    // display layers, in the order the core writes them
    private const int Background = 0;
    private const int Layer0 = 2;

    private static async Task<Emulator> Run(int mode)
    {
        var emulator = new Emulator();

        // an 8bpp bitmap on layer 0, its first 16 lines colour 1, and a red border (colour 2). The frames are waited for
        // with the VSYNC interrupt, so this also checks it still happens with the output off.
        await X16TestHelper.Emulate(@$"
                .machine CommanderX16R40
                .org $810
                    sei

                    lda #02
                    sta DC_BORDER

                    lda #$10
                    sta ADDRx_H
                    stz ADDRx_M
                    stz ADDRx_L
                    lda #1
                    ldy #20             ; 20 * 256 = 16 lines of 320
                    ldx #0
                .fill:
                    sta DATA0
                    dex
                    bne fill
                    dey
                    bne fill

                    lda #$07            ; bitmap, 8bpp
                    sta L0_CONFIG
                    stz L0_TILEBASE     ; address 0, 320 wide

                    lda #128
                    sta DC_HSCALE
                    sta DC_VSCALE

                    lda #$02
                    sta CTRL
                    lda #100            ; the border from x = 400, so it's on screen
                    sta DC_HSTOP
                    stz CTRL

                    lda #${0x10 | mode:X2}     ; layer 0, and the output mode
                    sta DC_VIDEO

                    lda #01
                    sta IEN
                    wai
                    sta ISR             ; clear interrupt and wait for second frame
                    wai

                    stp",
                emulator);

        return emulator;
    }

    private static Span<PixelRgba> DisplayLayer(Emulator emulator, int layer) => emulator.Display.Slice(Width * Height * layer, Width * Height);

    [TestMethod]
    public async Task Off_Black()
    {
        using var emulator = await Run(0);

        var black = new PixelRgba(0, 0, 0, 0xff);
        var transparent = new PixelRgba(0, 0, 0, 0);

        Assert.IsTrue(emulator.Vera.Frame_Count >= 2, $"Frames should still go by, {emulator.Vera.Frame_Count}.");

        // the visible area, the only part that's drawn
        for (var layer = 0; layer < 6; layer++)
        {
            var pixels = DisplayLayer(emulator, layer);
            var expected = layer == Background ? black : transparent;

            for (var y = 0; y < 480; y++)
                for (var x = 0; x < 640; x++)
                    Assert.AreEqual(expected, pixels[y * Width + x], $"Display layer {layer} at {x},{y}.");
        }
    }

    // VERA picks the output by the mode on every pixel clock, so turning it off and on part way down a frame blacks out
    // just the lines in between. Off at line 100 and on again at line 200, waiting for each with the LINE flag. Palette
    // entry 0 is set, so the background in the active area isn't black.
    [TestMethod]
    public async Task OffMidFrame_OnlyThoseLinesBlack()
    {
        using var emulator = new Emulator();

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                    sei

                    ; colour 0
                    lda #$01
                    sta ADDRx_H
                    lda #$fa
                    sta ADDRx_M
                    stz ADDRx_L
                    lda #$12
                    sta DATA0

                    lda #$01            ; output on, no layers
                    sta DC_VIDEO
                    lda #$03            ; flags only, the CPU ignores the interrupts
                    sta IEN

                    ; the start of a frame
                    lda #$01
                    sta ISR
                .vsync_1:
                    lda ISR
                    and #$01
                    beq vsync_1

                    lda #100
                    sta IRQLINE_L
                    lda #$02
                    sta ISR
                .line_100:
                    lda ISR
                    and #$02
                    beq line_100
                    stz DC_VIDEO        ; off

                    lda #200
                    sta IRQLINE_L
                    lda #$02
                    sta ISR
                .line_200:
                    lda ISR
                    and #$02
                    beq line_200
                    lda #$01            ; on
                    sta DC_VIDEO

                    ; the end of the frame
                    lda #$01
                    sta ISR
                .vsync_2:
                    lda ISR
                    and #$01
                    beq vsync_2

                    stp",
                emulator);

        var background = DisplayLayer(emulator, Background);
        var black = new PixelRgba(0, 0, 0, 0xff);
        var colour0 = emulator.Palette[0];
        Assert.AreNotEqual(black, colour0);

        // from the middle of each line, as the CPU takes a few pixels to see the flag and write DC_VIDEO
        for (var y = 0; y < 480; y++)
        {
            var expected = y >= 100 && y < 200 ? black : colour0;
            Assert.AreEqual(expected, background[y * Width + 320], $"Line {y}.");
        }
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public async Task On_Picture(int mode)
    {
        using var emulator = await Run(mode);

        var palette = emulator.Palette;

        Assert.AreEqual(palette[1], DisplayLayer(emulator, Layer0)[4 * Width + 100], "The bitmap.");
        Assert.AreEqual(palette[2], DisplayLayer(emulator, Background)[4 * Width + 500], "The border.");
    }
}
