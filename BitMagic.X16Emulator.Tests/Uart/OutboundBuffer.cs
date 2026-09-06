using BitMagic.X16Emulator.TestHelper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Uart;

[TestClass]
public class OutboundBuffer
{
    private const byte LsrEmptyMask = 0b01100000; // THRE | TEMT, as defined by LSR_Empty in Uart.asm

    // Writes `count` bytes (values 0x00, 0x01, 0x02, ...) to $9fe0 in sequence. Each
    // iteration is 5 bytes (lda #imm = 2, sta $9fe0 = 3), so the trailing stp lands at
    // $810 + 5*count.
    private static string WriteBytesCode(int count)
    {
        var code = @"
                .machine CommanderX16R40
                .org $810
";
        for (var i = 0; i < count; i++)
            code += $"                lda #${i:X2}\n                sta $9fe0\n";
        code += "                stp";
        return code;
    }

    [TestMethod]
    public async Task Outbound_FillsExactlySixteen()
    {
        var emulator = X16TestHelper.NewEmulator();

        await X16TestHelper.Emulate(WriteBytesCode(16), emulator);

        emulator.AssertState(Pc: 0x810 + 5 * 16 + 1); // stp at $860 -> PC $861
        Assert.IsFalse(emulator.Uart.EmptyOutbound);
        Assert.AreEqual(0u, emulator.Uart.ReadIndexOutbound);
        Assert.AreEqual(0u, emulator.Uart.WriteIndexOutbound); // wrapped back round after 16 writes

        AssertOutboundBufferStartsWith(emulator, Enumerable.Range(0, 16).Select(i => (byte)i).ToArray());
    }

    [TestMethod]
    public async Task Outbound_Overrun_DropsExtraByteWithoutCorruptingBuffer()
    {
        var emulator = X16TestHelper.NewEmulator();

        // 16 fill the FIFO exactly; the 17th (value 0x10) should be silently dropped.
        await X16TestHelper.Emulate(WriteBytesCode(17), emulator);

        emulator.AssertState(Pc: 0x810 + 5 * 17 + 1); // stp at $865 -> PC $866
        Assert.IsFalse(emulator.Uart.EmptyOutbound);
        Assert.AreEqual(0u, emulator.Uart.ReadIndexOutbound);
        Assert.AreEqual(0u, emulator.Uart.WriteIndexOutbound); // unchanged from the full-16 state

        AssertOutboundBufferStartsWith(emulator, 0x00); // the dropped 17th byte must not have overwritten slot 0
    }

    [TestMethod]
    public async Task Outbound_Overrun_ExtraByteNeverReachesModemEvenAfterDraining()
    {
        var mock = new MockZiModemHost();
        var emulator = new Emulator(new EmulatorOptions { ZiModemHostOverride = mock.Exports });

        // Fill the FIFO exactly (16 bytes: 0x00-0x0F), then write a 17th (0x10) that
        // should be silently dropped. Nothing drains during this stage -- same reasoning
        // as the other drain tests: the one free tick is spent on an early LDA, before
        // the first STA ever makes empty_outbound false, so the fill/overrun happens
        // undisturbed.
        await X16TestHelper.Emulate(WriteBytesCode(17), emulator);

        emulator.AssertState(Pc: 0x810 + 5 * 17 + 1); // stp at $865 -> PC $866
        Assert.IsFalse(emulator.Uart.EmptyOutbound);
        Assert.AreEqual(0, mock.SentBytes.Count); // confirms nothing has drained yet

        // Now force fast ticks and drain everything actually sitting in the FIFO.
        emulator.Uart.CpuTicks = 1;
        emulator.ClockUart = 0;

        var drainCode = @"
                .machine CommanderX16R40
                .org $810
";
        for (var i = 0; i < 20; i++)
            drainCode += "                nop\n";
        drainCode += "                stp";

        await X16TestHelper.Emulate(drainCode, emulator);

        emulator.AssertState(Pc: 0x810 + 20 + 1); // 20 nops then stp at $824 -> PC $825
        // Only the 16 bytes that actually fit ever reach the modem -- the 17th (0x10) is
        // gone for good, not just "didn't corrupt the buffer" at one snapshot in time.
        CollectionAssert.AreEqual(Enumerable.Range(0, 16).Select(i => (byte)i).ToArray(), mock.SentBytes.ToArray());
        Assert.IsTrue(emulator.Uart.EmptyOutbound);
    }

    // Span<byte> (what Emulator.Uart.BufferOutbound returns) can't be used as a local in an
    // async method under this project's C# version -- keep it confined to a plain method.
    private static void AssertOutboundBufferStartsWith(Emulator emulator, params byte[] expected)
    {
        var buffer = emulator.Uart.BufferOutbound;
        for (var i = 0; i < expected.Length; i++)
            Assert.AreEqual(expected[i], buffer[i], $"byte at index {i}");
    }

    [TestMethod]
    public async Task Outbound_Write_ClearsThreAndTemt()
    {
        var emulator = X16TestHelper.NewEmulator();
        emulator.Memory[0x9fe5] = LsrEmptyMask; // simulate an idle/drained UART

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #$41
                sta $9fe0
                stp",
                emulator);

        emulator.AssertState(Pc: 0x816);
        Assert.AreEqual(0, emulator.Memory[0x9fe5] & LsrEmptyMask);
    }

    [TestMethod]
    public async Task Outbound_Drain_SendsQueuedByteToModem()
    {
        var mock = new MockZiModemHost();
        var emulator = new Emulator(new EmulatorOptions { ZiModemHostOverride = mock.Exports });

        // Stage 1: nothing queued yet, so it's harmless for the free first tick (clock_uart
        // starts at 0, always fires once) to land here and lock in the slow default
        // schedule -- unlike inbound, the outbound condition (empty_outbound=false) is
        // only created by 6502 code below, so that one free opportunity is guaranteed to
        // be spent before there's anything to drain if we don't do this in two stages.
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp",
                emulator);

        emulator.AssertState(Pc: 0x811);

        // Force the tick interval down and rewind the schedule (see InboundBuffer.cs for
        // why both are needed) before queuing anything, so the byte this stage writes has
        // a real chance to actually drain.
        emulator.Uart.CpuTicks = 1;
        emulator.ClockUart = 0;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #$41
                sta $9fe0
                nop
                nop
                stp",
                emulator);

        emulator.AssertState(Pc: 0x818);
        Assert.AreEqual(1, mock.SentBytes.Count);
        Assert.AreEqual((byte)0x41, mock.SentBytes[0]);
        Assert.IsTrue(emulator.Uart.EmptyOutbound);
    }

    [TestMethod]
    public async Task Outbound_Drain_SendsMultipleBytesInOrder()
    {
        var mock = new MockZiModemHost();
        var emulator = new Emulator(new EmulatorOptions { ZiModemHostOverride = mock.Exports });

        // Stage 1: same reasoning as the single-byte test above -- let the free tick spend
        // itself here, before anything exists to drain.
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp",
                emulator);

        emulator.AssertState(Pc: 0x811);

        emulator.Uart.CpuTicks = 1;
        emulator.ClockUart = 0;

        // Queue three bytes, then enough per-instruction checks to drain all of them --
        // one byte per tick, same as the inbound side.
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #$01
                sta $9fe0
                lda #$02
                sta $9fe0
                lda #$03
                sta $9fe0
                nop
                nop
                nop
                nop
                stp",
                emulator);

        emulator.AssertState(Pc: 0x824);
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x02, 0x03 }, mock.SentBytes.ToArray());
        Assert.IsTrue(emulator.Uart.EmptyOutbound);
    }

    // --- THRE interrupt: unlike RDA (uart_tick/uart_after_read/uart_fcr_write/
    // uart_dlm_ier_write all participate in raising and clearing it), nothing in the
    // core currently ever sets or clears IIR_THRE or INTERRUPT_UART_THRE -- LSR's
    // THRE/TEMT bits are maintained (see Outbound_Write_ClearsThreAndTemt above and
    // uart_tick's LSR_Empty set on drain), but the actual interrupt condition (IIR bit
    // 1, state.interrupt_hit's UartThre flag) is never touched anywhere. Both tests
    // below are expected to FAIL until that's implemented, mirroring how RDA does it:
    // assert immediately when THRE is enabled while the outbound FIFO is already empty
    // (the reset/idle state), clear once a byte is queued, and reassert once it drains.

    [TestMethod]
    public async Task Outbound_ThreInterrupt_ClearsWhenByteQueuedAndReassertsOnceDrained()
    {
        var mock = new MockZiModemHost();
        var emulator = new Emulator(new EmulatorOptions { ZiModemHostOverride = mock.Exports });

        // Stage 1: enable THRE with nothing ever queued -- the outbound FIFO starts
        // empty (uart_init sets empty_outbound = 1), so this should assert immediately,
        // same reasoning as RDA's "already satisfied when enabled" case. SEI first --
        // this checks state.Interrupt_Hit directly and, once implemented, this will make
        // the interrupt go live for real, so the CPU must never actually act on it (no
        // vector table here -- see Outbound_ThreInterrupt_VectorsToHandler for that).
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sei
                lda #%00000010
                sta $9fe1      ; IER: enable THRE
                stp",
                emulator);

        emulator.AssertState(Pc: 0x817);
        Assert.IsTrue(emulator.Uart.InterruptThreEnabled);
        Assert.AreNotEqual(0, emulator.Memory[0x9fe2] & 0b0010, "IIR: THRE should be pending -- outbound is already empty");
        Assert.IsTrue((emulator.State.Interrupt_Hit & (uint)InterruptSource.UartThre) != 0,
            "enabling THRE while the outbound FIFO is already empty should immediately assert the interrupt");

        // Stage 2: queue a byte. The FIFO is no longer empty, so both the LSR THRE/TEMT
        // bits (already covered by Outbound_Write_ClearsThreAndTemt) and the interrupt
        // condition itself should clear.
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #$41
                sta $9fe0
                stp",
                emulator);

        emulator.AssertState(Pc: 0x816);
        Assert.AreEqual(0, emulator.Memory[0x9fe2] & 0b0010, "IIR: THRE must clear once a byte is queued");
        Assert.IsTrue((emulator.State.Interrupt_Hit & (uint)InterruptSource.UartThre) == 0);

        // Stage 3: force fast ticks and let the queued byte actually drain -- once the
        // FIFO empties again, THRE should reassert.
        emulator.Uart.CpuTicks = 1;
        emulator.ClockUart = 0;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                nop
                nop
                stp",
                emulator);

        emulator.AssertState(Pc: 0x813);
        Assert.IsTrue(emulator.Uart.EmptyOutbound);
        Assert.AreNotEqual(0, emulator.Memory[0x9fe2] & 0b0010, "IIR: THRE should reassert once the FIFO drains back to empty");
        Assert.IsTrue((emulator.State.Interrupt_Hit & (uint)InterruptSource.UartThre) != 0);
    }

    [TestMethod]
    public async Task Outbound_ThreInterrupt_VectorsToHandler()
    {
        var mock = new MockZiModemHost();
        var emulator = new Emulator(new EmulatorOptions { ZiModemHostOverride = mock.Exports });

        // IRQ vector -> $0900, same technique as Inbound_RdaInterrupt_VectorsToHandler.
        emulator.RomBank[0x3ffe] = 0x00;
        emulator.RomBank[0x3fff] = 0x09;

        // No SEI -- the outbound FIFO is already empty (nothing ever queued), so
        // enabling THRE should make the interrupt go live for real immediately, and we
        // want the CPU to actually take it.
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #%00000010
                sta $9fe1      ; IER: enable THRE
                stp
                .org $900
                lda #$00
                sta $9fe1      ; disable THRE -- otherwise RTI immediately refires (the
                               ; FIFO is still empty; nothing here drains anything the
                               ; way reading $9fe0 does for RDA)
                lda #$ab       ; marker: only reachable if the CPU actually vectored here
                rti",
                emulator);

        // Proves the CPU actually vectored to $900 and ran the handler (and, via Pc,
        // that RTI correctly resumed and completed the interrupted main flow back at
        // $810 -- landing on the stp that was interrupted before it ever executed).
        emulator.AssertState(A: 0xab, Pc: 0x816);
        Assert.IsFalse(emulator.Interrupt);
        Assert.IsTrue((emulator.State.Interrupt_Hit & (uint)InterruptSource.UartThre) == 0);
    }

    [TestMethod]
    public async Task Outbound_ThreInterrupt_ReadingIirAcknowledgesIt()
    {
        // Per the TL16C2550 datasheet, THRE's acknowledgment mechanism is different from
        // RDA's: RDA clears by draining the RX FIFO (reading $9fe0 below the trigger),
        // but THRE clears simply by the CPU reading IIR while THRE is the reported
        // interrupt source -- nothing about the outbound FIFO itself needs to change.
        //
        // This test is scoped to just that reset behaviour (uart_iir_afterread), not to
        // whatever raises THRE in the first place -- that's separate functionality
        // (see Outbound_ThreInterrupt_ClearsWhenByteQueuedAndReassertsOnceDrained and
        // Outbound_ThreInterrupt_VectorsToHandler), and depending on it here would mean
        // this test can't even reach its own assertions until that's implemented. So the
        // "already pending" precondition is poked directly instead of produced for real.
        var emulator = X16TestHelper.NewEmulator();

        emulator.Memory[0x9fe2] = 0b0010;                    // IIR: THRE pending
        emulator.InterruptHit = InterruptSource.UartThre;    // the live interrupt itself

        // The I flag must be set directly here, not via an in-code SEI -- interrupt_hit
        // is already live before Emulate() runs its first instruction, so with no vector
        // table set up here, an in-code SEI would never get the chance to execute before
        // the CPU tried to service it (this test only cares about the state change a
        // read produces, not real vectoring). The outbound FIFO is never touched by any
        // of this, isolating "reading IIR acknowledges THRE" from the FIFO's own
        // empty/non-empty state.
        emulator.InterruptDisable = true;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda $9fe2
                stp",
                emulator);

        emulator.AssertState(Pc: 0x814);
        Assert.IsTrue(emulator.Uart.EmptyOutbound, "the FIFO itself was never touched by this test");
        Assert.AreEqual(0, emulator.Memory[0x9fe2] & 0b0010, "IIR: THRE must clear once it has been read");
        Assert.IsTrue((emulator.State.Interrupt_Hit & (uint)InterruptSource.UartThre) == 0,
            "reading IIR should acknowledge and clear the THRE interrupt");
    }
}
