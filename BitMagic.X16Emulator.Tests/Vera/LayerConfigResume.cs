using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Vera;

// vera_init runs each time the core is entered, eg when the debugger resumes, and rebuilds L0_CONFIG and L1_CONFIG
// (and each layer's renderer) from the state. L0_CONFIG used to take layer 1's T256C bit.
[TestClass]
public class LayerConfigResume
{
    [TestMethod]
    [DataRow(0x00, 0x08)]
    [DataRow(0x08, 0x00)]
    [DataRow(0x08, 0x08)]
    [DataRow(0xf1, 0x08)]
    [DataRow(0x08, 0xf1)]
    public async Task ConfigKeptAfterResume(int l0Config, int l1Config)
    {
        using var emulator = new Emulator();

        await X16TestHelper.Emulate(@$"
                .machine CommanderX16R40
                .org $810
                lda #{l0Config}
                sta L0_CONFIG
                lda #{l1Config}
                sta L1_CONFIG
                stp
                stp",
                emulator);

        emulator.EmulateWithTimeout();

        Assert.AreEqual(l0Config, emulator.Memory[0x9f2d], "L0_CONFIG");
        Assert.AreEqual(l1Config, emulator.Memory[0x9f34], "L1_CONFIG");
        Assert.AreEqual((l0Config & 0x08) != 0, emulator.Vera.Layer0_T256C, "Layer 0 T256C");
        Assert.AreEqual((l1Config & 0x08) != 0, emulator.Vera.Layer1_T256C, "Layer 1 T256C");
    }

    // vera_init also picks layer 0's renderer from L0_CONFIG. With 1bpp tiles T256C changes the colours: off, the
    // attribute $21 is foreground 1 on background 2; on, it's foreground $21. Layer 1 has T256C on but isn't shown, so
    // layer 0 should still draw foreground 1 after a resume.
    [TestMethod]
    public async Task Layer0RendererAfterResume()
    {
        using var emulator = new Emulator();

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                    sei

                    ; tile 0 at $00000, all foreground
                    lda #$10
                    sta ADDRx_H
                    stz ADDRx_M
                    stz ADDRx_L
                    ldx #8
                    lda #$ff
                .tile:
                    sta DATA0
                    dex
                    bne tile

                    ; map at $01000, first entry is tile 0 with attribute $21
                    lda #$10
                    sta ADDRx_M
                    stz ADDRx_L
                    stz DATA0
                    lda #$21
                    sta DATA0

                    stz L0_CONFIG   ; 32x32 map, tiles, 1bpp, no T256C
                    lda #$08
                    sta L0_MAPBASE  ; $01000
                    stz L0_TILEBASE ; $00000, 8x8

                    lda #$08        ; T256C on layer 1 only
                    sta L1_CONFIG

                    lda #128
                    sta DC_HSCALE
                    sta DC_VSCALE

                    lda #$11        ; layer 0 only
                    sta DC_VIDEO

                    stp             ; the test resumes from here

                    lda #01
                    sta IEN
                    wai
                    sta ISR         ; clear interrupt and wait for second frame
                    wai

                    stp",
                emulator);

        emulator.EmulateWithTimeout();

        var palette = emulator.Palette;
        Assert.AreNotEqual(palette[0x01], palette[0x21], "The two colours need to differ for this to prove anything.");

        var layer0 = emulator.Display.Slice(800 * 525 * 2, 800 * 525);   // background, sprite layer 1, then layer 0

        Assert.AreEqual(palette[0x01], layer0[4 * 800 + 4]);
    }
}
