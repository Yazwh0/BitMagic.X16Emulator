using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Uart;

[TestClass]
public class Msr
{
    // MSR ($9fe6) in this emulation is a fixed status byte. uart_init seeds CTS (bit 4)
    // and DSR (bit 5) active; nothing on the modem side ever changes them. RI (bit 6)
    // and DCD (bit 7) stay clear -- no inbound-call / carrier modelling. Reads clear the
    // delta bits (0-3, uart_msr_afterread: "and r12b, 11110000b") but leave the top
    // nibble intact; writes are discarded (uart_msr_write restores the previous byte).
    private const byte CtsAndDsr = 0b0011_0000;

    [TestMethod]
    public async Task Msr_InitialState_CtsAndDsrAsserted()
    {
        var emulator = X16TestHelper.NewEmulator();

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp",
                emulator);

        emulator.AssertState(Pc: 0x811);
        Assert.AreEqual(CtsAndDsr, emulator.Memory[0x9fe6],
            "uart_init should seed MSR with CTS + DSR asserted and nothing else");
    }

    [TestMethod]
    public async Task Msr_ReadByCpu_ReturnsCtsAndDsr()
    {
        var emulator = X16TestHelper.NewEmulator();

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda $9fe6
                stp",
                emulator);

        emulator.AssertState(A: CtsAndDsr, Pc: 0x814);
    }

    [TestMethod]
    public async Task Msr_RepeatedReads_StayStable()
    {
        // Reading MSR clears the delta bits (0-3). CTS/DSR are in the top nibble, so the
        // delta-clear must leave them untouched -- a second read still sees the same value.
        var emulator = X16TestHelper.NewEmulator();

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda $9fe6
                lda $9fe6
                stp",
                emulator);

        emulator.AssertState(A: CtsAndDsr, Pc: 0x817);
        Assert.AreEqual(CtsAndDsr, emulator.Memory[0x9fe6],
            "the top nibble (CTS/DSR) must survive the post-read delta-bit clear");
    }

    [TestMethod]
    public async Task Msr_WriteWithAllBitsSet_IsIgnored()
    {
        // MSR is read-only on a real 16550 -- uart_msr_write restores the pre-write byte.
        var emulator = X16TestHelper.NewEmulator();

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #$ff
                sta $9fe6
                lda $9fe6
                stp",
                emulator);

        emulator.AssertState(A: CtsAndDsr, Pc: 0x819);
        Assert.AreEqual(CtsAndDsr, emulator.Memory[0x9fe6],
            "writing $ff to MSR must not change it");
    }

    [TestMethod]
    public async Task Msr_WriteWithAllBitsClear_IsIgnored()
    {
        var emulator = X16TestHelper.NewEmulator();

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #$00
                sta $9fe6
                lda $9fe6
                stp",
                emulator);

        emulator.AssertState(A: CtsAndDsr, Pc: 0x819);
        Assert.AreEqual(CtsAndDsr, emulator.Memory[0x9fe6],
            "writing $00 to MSR must not clear CTS/DSR");
    }
}
