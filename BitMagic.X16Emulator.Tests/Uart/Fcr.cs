using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Uart;

[TestClass]
public class Fcr
{
    [TestMethod]
    public async Task Fcr_TriggerLevel_1()
    {
        var emulator = X16TestHelper.NewEmulator();

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #%00000000
                sta $9fe2
                stp",
                emulator);

        Assert.AreEqual(1u, emulator.Uart.FifoTrigger);
    }

    [TestMethod]
    public async Task Fcr_TriggerLevel_4()
    {
        var emulator = X16TestHelper.NewEmulator();

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #%01000000
                sta $9fe2
                stp",
                emulator);

        Assert.AreEqual(4u, emulator.Uart.FifoTrigger);
    }

    [TestMethod]
    public async Task Fcr_TriggerLevel_8()
    {
        var emulator = X16TestHelper.NewEmulator();

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #%10000000
                sta $9fe2
                stp",
                emulator);

        Assert.AreEqual(8u, emulator.Uart.FifoTrigger);
    }

    [TestMethod]
    public async Task Fcr_TriggerLevel_14()
    {
        var emulator = X16TestHelper.NewEmulator();

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #%11000000
                sta $9fe2
                stp",
                emulator);

        Assert.AreEqual(14u, emulator.Uart.FifoTrigger);
    }

    [TestMethod]
    public async Task Fcr_DoesNotDisturbIir()
    {
        // $9fe2 is read as IIR, written as FCR -- writing FCR must restore whatever byte
        // was already there (uart_fcr_write's "preserve the current value" behaviour),
        // not leave the CPU's written value sitting in memory.
        var emulator = X16TestHelper.NewEmulator();
        emulator.Memory[0x9fe2] = 0x01; // pretend RDA is latched in IIR

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #%11000000
                sta $9fe2
                stp",
                emulator);

        Assert.AreEqual(14u, emulator.Uart.FifoTrigger);
        Assert.AreEqual((byte)0x01, emulator.Memory[0x9fe2]);
    }
}
