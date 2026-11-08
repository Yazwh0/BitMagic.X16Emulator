using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Vera.Display;

// Which bitmap line each display line shows, with a display start and a vertical scale. From the VERA Verilog
// (vera-module, composer.v): the line counter is set to 0 on the first active line (DC_VSTART, in 2 line units), then
// has DC_VSCALE added each line, and the line drawn is counter >> 7. Lines are rendered one ahead, so the first active
// line shows line 0.
//
// The bitmap is 8bpp and 320 wide, line k filled with colour 16 + k (the grey ramp), so the colour of a display line
// says which bitmap line it shows.
[TestClass]
public class VstartLines
{
    private const int BitmapLines = 16;
    private const int FirstColour = 16;
    private const int X = 100;

    [TestMethod]
    [DataRow(0, 128)]
    [DataRow(1, 128)]
    [DataRow(10, 128)]
    [DataRow(0, 64)]
    [DataRow(10, 64)]
    [DataRow(10, 32)]
    [DataRow(10, 96)]      // 0.75, so the fraction carries from the first line
    [DataRow(10, 200)]     // more than 1, lines skipped
    public async Task Layer0(int vstart, int vscale)
    {
        using var emulator = new Emulator();

        await X16TestHelper.Emulate(@$"
                .machine CommanderX16R40
                .org $810
                    sei

                    lda #02
                    sta DC_BORDER

                    ; bitmap: line k is all colour 16 + k
                    lda #$10
                    sta ADDRx_H
                    stz ADDRx_M
                    stz ADDRx_L

                    lda #{FirstColour}
                .line:
                    ldy #2
                .half:
                    ldx #160
                .pixel:
                    sta DATA0
                    dex
                    bne pixel
                    dey
                    bne half
                    clc
                    adc #1
                    cmp #{FirstColour + BitmapLines}
                    bne line

                    lda #$07        ; bitmap, 8bpp
                    sta L0_CONFIG
                    stz L0_TILEBASE ; address 0, 320 wide

                    lda #128
                    sta DC_HSCALE
                    lda #{vscale}
                    sta DC_VSCALE

                    lda #$02
                    sta CTRL
                    lda #{vstart}
                    sta DC_VSTART
                    stz CTRL

                    lda #$11        ; layer 0
                    sta DC_VIDEO

                    lda #01
                    sta IEN
                    wai
                    sta ISR     ; clear interrupt and wait for second frame
                    wai

                    stp",
                emulator);

        var colours = emulator.Palette.Slice(FirstColour, BitmapLines).ToArray();
        Assert.AreEqual(BitmapLines, colours.Distinct().Count(), "The bitmap's colours need to be different to tell its lines apart.");

        var layer0 = emulator.Display.Slice(800 * 525 * 2, 800 * 525);   // background, sprite layer 1, then layer 0

        var firstLine = vstart * 2;

        if (firstLine > 0)
            Assert.AreEqual(new Common.PixelRgba(0, 0, 0, 0), layer0[(firstLine - 1) * 800 + X], $"Line {firstLine - 1} is above the display start.");

        for (var k = 0; ; k++)
        {
            var bitmapLine = (k * vscale) >> 7;
            if (bitmapLine >= BitmapLines)
                break;

            var y = firstLine + k;
            var actual = layer0[y * 800 + X];
            var shown = Array.IndexOf(colours, actual);
            Assert.AreEqual(bitmapLine, shown, $"Display line {y} ({k} after the start) should show bitmap line {bitmapLine}.");
        }
    }
}
