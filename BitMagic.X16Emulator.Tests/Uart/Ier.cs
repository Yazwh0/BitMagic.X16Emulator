using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Uart;

[TestClass]
public class Ier
{
    [TestMethod]
    public async Task Ier_RdaOnly()
    {
        var emulator = X16TestHelper.NewEmulator();

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #%00000001
                sta $9fe1
                stp",
                emulator);

        Assert.IsTrue(emulator.Uart.InterruptRdaEnabled);
        Assert.IsFalse(emulator.Uart.InterruptThreEnabled);
    }

    [TestMethod]
    public async Task Ier_ThreOnly()
    {
        var emulator = X16TestHelper.NewEmulator();

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #%00000010
                sta $9fe1
                stp",
                emulator);

        Assert.IsFalse(emulator.Uart.InterruptRdaEnabled);
        Assert.IsTrue(emulator.Uart.InterruptThreEnabled);
    }

    [TestMethod]
    public async Task Ier_Both()
    {
        var emulator = X16TestHelper.NewEmulator();

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #%00000011
                sta $9fe1
                stp",
                emulator);

        Assert.IsTrue(emulator.Uart.InterruptRdaEnabled);
        Assert.IsTrue(emulator.Uart.InterruptThreEnabled);
    }

    [TestMethod]
    public async Task Ier_Neither()
    {
        var emulator = X16TestHelper.NewEmulator();

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #%00000000
                sta $9fe1
                stp",
                emulator);

        Assert.IsFalse(emulator.Uart.InterruptRdaEnabled);
        Assert.IsFalse(emulator.Uart.InterruptThreEnabled);
    }
}
