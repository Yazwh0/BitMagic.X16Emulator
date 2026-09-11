using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Uart;

[TestClass]
public class Mcr
{
    // MCR ($9fe4) is not acted on, but it must still behave as a register: a write
    // stores the byte (uart_mcr_write forces bit 2, OUT1, to 0), and a read returns it.
    //
    // Known bug: io_r_9fe4 is wired to uart_msr_afterread, which masks a stale r12b into
    // $9fe4 on every read, corrupting the register. The round-trip tests below fail until
    // io_r_9fe4 is pointed at io_r_readmemory.

    [TestMethod]
    public async Task Mcr_WriteStoresTheByte_Bit2Cleared()
    {
        // Write side only (uart_mcr_write): $ff in, bit 2 forced off, so $fb is stored.
        var emulator = X16TestHelper.NewEmulator();

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #$ff
                sta $9fe4
                stp",
                emulator);

        emulator.AssertState(Pc: 0x816);
        Assert.AreEqual((byte)0xfb, emulator.Memory[0x9fe4]);
    }

    [TestMethod]
    public async Task Mcr_ReadDoesNotCorruptTheRegister()
    {
        // A CPU read of $9fe4 must leave the stored value in place. The read's after-read
        // handler must not rewrite the byte.
        var emulator = X16TestHelper.NewEmulator();

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #$0b
                sta $9fe4
                lda $9fe4
                stp",
                emulator);

        emulator.AssertState(A: 0x0b, Pc: 0x819);
        Assert.AreEqual((byte)0x0b, emulator.Memory[0x9fe4],
            "reading MCR must not change it");
    }

    [TestMethod]
    public async Task Mcr_ReadsBackAcrossRepeatedReads()
    {
        // The first read latches A before any after-read handler runs, so a single read
        // can look fine even when the handler corrupts memory. Read twice: the second
        // read sees whatever the first one left behind.
        var emulator = X16TestHelper.NewEmulator();

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #$0b
                sta $9fe4
                lda #$00
                lda $9fe4
                lda #$00
                lda $9fe4
                stp",
                emulator);

        emulator.AssertState(A: 0x0b, Pc: 0x820);
        Assert.AreEqual((byte)0x0b, emulator.Memory[0x9fe4]);
    }
}
