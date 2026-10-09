using BitMagic.X16Emulator.TestHelper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Vera;

/// <summary>
/// Changing a layer's mode while the line is being drawn must restart the layer's fetch in the new mode. The fetch made
/// in the old mode isn't usable by the new renderer: with 1bpp 16 wide tiles the position through the tile can be 8-15,
/// which is past the end of the 4bpp renderer's jump table.
/// </summary>
[TestClass]
public class LayerConfigMidLine
{
    [TestMethod]
    public async Task OneBpp16Wide_To_FourBpp_MidLine()
    {
        var emulator = new Emulator();

        await X16TestHelper.Emulate(@"
            .machine CommanderX16R42
            .org $810
                lda #$11
                sta DC_VIDEO        ; VGA, layer 0 on
                lda #$01
                sta L0_TILEBASE     ; tile base 0, 16 wide tiles
                lda #9
                sta L0_HSCROLL_L    ; so the first fetch of each line is 9 pixels into a tile

                ldy #$00
                ldx #$00
            .loop:
                lda #$00
                sta L0_CONFIG       ; 1bpp tiles
                nop
                nop
                lda #$02
                sta L0_CONFIG       ; 4bpp tiles
                nop
                dex
                bne loop
                dey
                bne loop

                stp", emulator);

        // got to the stp, at $836, without a crash
        emulator.AssertState(Pc: 0x837);
    }
}
