using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Uart;

[TestClass]
public class Scr
{
    // SCR ($9fe7) is a plain scratch register: the 16550 has no functional use for it,
    // it just has to store whatever byte the CPU writes and hand it back on read. The
    // dispatch entries are io_r_readmemory / io_w_unsupported (both no-ops), so the CPU's
    // write lands in memory and nothing touches it after -- unlike the write-only UART
    // registers, which restore the previous value.

    [TestMethod]
    public async Task Scr_HoldsWrittenValue()
    {
        var emulator = X16TestHelper.NewEmulator();

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #$5a
                sta $9fe7
                lda #$00       ; clobber A so the readback is meaningful
                lda $9fe7
                stp",
                emulator);

        emulator.AssertState(A: 0x5a, Pc: 0x81b);
        Assert.AreEqual((byte)0x5a, emulator.Memory[0x9fe7]);
    }

    [TestMethod]
    public async Task Scr_HoldsFullByte()
    {
        var emulator = X16TestHelper.NewEmulator();

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #$ff
                sta $9fe7
                lda #$00
                lda $9fe7
                stp",
                emulator);

        emulator.AssertState(A: 0xff, Pc: 0x81b);
    }

    [TestMethod]
    public async Task Scr_SecondWriteReplacesTheFirst()
    {
        var emulator = X16TestHelper.NewEmulator();

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #$5a
                sta $9fe7
                lda #$a5
                sta $9fe7
                lda #$00
                lda $9fe7
                stp",
                emulator);

        emulator.AssertState(A: 0xa5, Pc: 0x820);
        Assert.AreEqual((byte)0xa5, emulator.Memory[0x9fe7]);
    }
}
