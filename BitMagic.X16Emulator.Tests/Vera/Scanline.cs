
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Vera;

[TestClass]
public class Scanline
{
    [TestMethod]
    public async Task Read()
    {
        var emulator = new Emulator();

        var (_, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                sei
                lda #50
                sta IRQLINE_L
                lda #2
                sta IEN

                wai

                lda SCANLINE_L
                ldx IEN
                stp
                ",
        emulator);

        Assert.IsTrue(emulator.A == 51);
        Assert.IsTrue(emulator.X == 2);     // just line interrupt
    }

    [TestMethod]
    public async Task Read_SecondHalf()
    {
        var emulator = new Emulator();

        var (_, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                sei
                lda #50
                sta IRQLINE_L
                lda #%1000_0010
                sta IEN
                wai

                lda SCANLINE_L
                ldx IEN
                stp
                ",
        emulator);

        emulator.DisplayState();

        Assert.IsTrue(emulator.A == 51);
        Assert.IsTrue(emulator.X == 0b1100_0010);     // just line interrupt
    }
}
