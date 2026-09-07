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

        emulator.AssertState(Pc: 0x816);
        Assert.IsTrue(emulator.Uart.InterruptRdaEnabled);
        Assert.IsFalse(emulator.Uart.InterruptThreEnabled);
    }

    [TestMethod]
    public async Task Ier_ThreOnly()
    {
        var emulator = X16TestHelper.NewEmulator();

        // SEI first -- the outbound FIFO starts genuinely idle (uart_init seeds
        // IIR_THRE), so enabling THRE here correctly asserts the interrupt for real
        // immediately (see Outbound_ThreInterrupt_ClearsWhenByteQueuedAndReassertsOnceDrained).
        // This test only cares about the enabled-flag readback, not real IRQ delivery,
        // and there's no vector table set up here, so the CPU must never actually act
        // on the pending IRQ.
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sei
                lda #%00000010
                sta $9fe1
                stp",
                emulator);

        emulator.AssertState(Pc: 0x817);
        Assert.IsFalse(emulator.Uart.InterruptRdaEnabled);
        Assert.IsTrue(emulator.Uart.InterruptThreEnabled);
    }

    [TestMethod]
    public async Task Ier_Both()
    {
        var emulator = X16TestHelper.NewEmulator();

        // SEI first -- same reasoning as Ier_ThreOnly: enabling THRE while the outbound
        // FIFO is idle now correctly asserts the interrupt for real, and this test only
        // cares about the enabled-flag readback.
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sei
                lda #%00000011
                sta $9fe1
                stp",
                emulator);

        emulator.AssertState(Pc: 0x817);
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

        emulator.AssertState(Pc: 0x816);
        Assert.IsFalse(emulator.Uart.InterruptRdaEnabled);
        Assert.IsFalse(emulator.Uart.InterruptThreEnabled);
    }

    [TestMethod]
    public async Task Ier_TopFourBitsAreNotStored()
    {
        // Unlike $9fe2 (FCR/IIR), IER genuinely is read == write -- there's no separate
        // register hiding behind the same address, so a write should still be visible on
        // a subsequent read (this only applies with DLAB clear; with the divisor latch
        // enabled, $9fe1 is the divisor MSB instead and the full byte is meaningful).
        // But real IER only implements the low 4 bits (RDA/THRE enables here, plus two
        // more not modelled); the top 4 bits are unused and must read back as 0
        // regardless of what the CPU wrote into them.
        var emulator = X16TestHelper.NewEmulator();

        // SEI first -- the low nibble here also enables THRE, and the outbound FIFO
        // starts genuinely idle, so this would otherwise fire a real interrupt (see
        // Ier_ThreOnly). This test only cares about the stored bits, not IRQ delivery.
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sei
                lda #%11110011
                sta $9fe1      ; IER: top nibble set, should be masked off on readback
                stp",
                emulator);

        emulator.AssertState(Pc: 0x817);
        Assert.IsTrue(emulator.Uart.InterruptRdaEnabled);
        Assert.IsTrue(emulator.Uart.InterruptThreEnabled);
        Assert.AreEqual((byte)0b0011, (byte)(emulator.Memory[0x9fe1] & 0b00001111),
            "the low nibble should reflect what was written");
        Assert.AreEqual(0, emulator.Memory[0x9fe1] & 0b11110000,
            "the top 4 bits of IER are unused and must not be stored");
    }
}
