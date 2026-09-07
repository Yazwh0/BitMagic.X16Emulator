using BitMagic.X16Emulator.TestHelper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Uart;

[TestClass]
public class InboundBuffer
{
    [TestMethod]
    public async Task Inbound_ByteArrives_IsReadableAndSetsDr()
    {
        var mock = new MockZiModemHost();
        var emulator = new Emulator(new EmulatorOptions { ZiModemHostOverride = mock.Exports });
        emulator.Uart.FifoEnabled = true; // FCR bit 0 -- required for FIFO reads/writes

        mock.EnqueueInbound(0x41);

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                nop
                nop
                stp",
                emulator);

        emulator.AssertState(Pc: 0x813);
        Assert.AreEqual((byte)0x41, emulator.Memory[0x9fe0]);
        Assert.AreNotEqual(0, emulator.Memory[0x9fe5] & 0b00000001); // DR
        Assert.IsFalse(emulator.Uart.EmptyInbound);
    }

    [TestMethod]
    public async Task Inbound_Read_ClearsDrOnceDrained()
    {
        var mock = new MockZiModemHost();
        var emulator = new Emulator(new EmulatorOptions { ZiModemHostOverride = mock.Exports });
        emulator.Uart.FifoEnabled = true; // FCR bit 0 -- required for FIFO reads/writes

        mock.EnqueueInbound(0x41);

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                nop
                nop
                lda $9fe0
                stp",
                emulator);

        emulator.AssertState(A: 0x41, Pc: 0x816); // the LDA actually read the delivered byte, not a stale 0
        Assert.AreEqual(0, emulator.Memory[0x9fe5] & 0b00000001); // DR cleared
        Assert.IsTrue(emulator.Uart.EmptyInbound);
    }

    [TestMethod]
    public async Task Inbound_RdaInterrupt_FiresOnceEnabledAndClearsOnceDrained()
    {
        var mock = new MockZiModemHost();
        var emulator = new Emulator(new EmulatorOptions { ZiModemHostOverride = mock.Exports });
        emulator.Uart.FifoEnabled = true; // FCR bit 0 -- required for FIFO reads/writes

        // Stage 1: no bytes exist yet, so it's safe for uart_tick to fire (and find
        // nothing to do) whenever it likes here -- no burn loop needed. Enable RDA
        // before anything is queued for it to fire on. SEI first -- this test checks
        // state.Interrupt_Hit directly, it isn't exercising real IRQ vectoring/servicing
        // (there's no vector table set up here), so the CPU must never actually act on
        // the pending IRQ.
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sei
                lda #%00000001
                sta $9fe1      ; IER: enable RDA
                stp",
                emulator);

        emulator.AssertState(Pc: 0x817);
        Assert.IsTrue(emulator.Uart.InterruptRdaEnabled);
        Assert.IsTrue((emulator.State.Interrupt_Hit & (uint)InterruptSource.UartRda) == 0);

        // IER is now guaranteed configured, and uart_init (which resets cpu_ticks) only
        // ever runs once -- so forcing the tick interval down here is safe and sticks for
        // the rest of this Emulator's life. Also rewind ClockUart: stage 1 still ran with
        // the slow default, so its own "free" first tick (clock_uart starts at 0, always
        // fires once) already scheduled the next real check ~61439 cycles out -- without
        // resetting it too, the next check wouldn't land until that stale, still-slow
        // threshold is reached, regardless of the now-fast CpuTicks. Queue a byte and let
        // a uart_tick pull it in; trigger level defaults to 0 (FCR never written), so any
        // count >= 1 satisfies it.
        emulator.Uart.CpuTicks = 1;
        emulator.ClockUart = 0;
        mock.EnqueueInbound(0x41);

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                nop
                nop
                stp",
                emulator);

        emulator.AssertState(Pc: 0x813);
        Assert.IsTrue((emulator.State.Interrupt_Hit & (uint)InterruptSource.UartRda) != 0);

        // Draining the only queued byte should drop the count back below the trigger,
        // clearing the interrupt again.
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda $9fe0
                stp",
                emulator);

        emulator.AssertState(Pc: 0x814);
        Assert.IsTrue((emulator.State.Interrupt_Hit & (uint)InterruptSource.UartRda) == 0);
    }

    [TestMethod]
    public async Task Inbound_RdaInterrupt_VectorsToHandler()
    {
        var mock = new MockZiModemHost();
        var emulator = new Emulator(new EmulatorOptions { ZiModemHostOverride = mock.Exports });
        emulator.Uart.FifoEnabled = true; // FCR bit 0 -- required for FIFO reads/writes

        // IRQ vector -> $0900. RomBank occupies the top of the address space, so $3ffe/
        // $3fff here are $fffe/$ffff -- same technique Vera/Interrupt_Vsync.cs uses.
        emulator.RomBank[0x3ffe] = 0x00;
        emulator.RomBank[0x3fff] = 0x09;

        // Stage 1: configure IER with nothing queued yet -- no burn loop needed, it's
        // safe for uart_tick to run (and find nothing to do) whenever it likes here.
        // Unlike the previous test, there's no SEI -- this one wants the CPU to actually
        // take the IRQ, so interrupts must stay enabled (the default/reset state already
        // has the I flag clear, matching every other interrupt test in this suite).
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #%00000001
                sta $9fe1      ; IER: enable RDA
                stp",
                emulator);

        emulator.AssertState(Pc: 0x816);
        Assert.IsTrue(emulator.Uart.InterruptRdaEnabled);

        // IER and the vector are now guaranteed configured, and uart_init (which resets
        // cpu_ticks) only ever runs once -- forcing the tick interval down here sticks
        // for the rest of this Emulator's life. Also rewind ClockUart -- stage 1's own
        // "free" first tick already scheduled the next real check far out using the
        // still-slow default, so without this the now-fast CpuTicks wouldn't matter until
        // that stale threshold is reached. Queue a byte and let uart_tick raise the
        // interrupt for real this time.
        emulator.Uart.CpuTicks = 1;
        emulator.ClockUart = 0;
        mock.EnqueueInbound(0x41);

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                nop
                nop
                stp
                .org $900
                lda $9fe0      ; drain the byte -- clears the RDA condition so it doesn't
                               ; immediately re-fire the moment RTI restores the I flag
                lda #$ab       ; marker: only reachable if the CPU actually vectored here
                rti",
                emulator);

        // Proves the CPU actually vectored to $900 and ran the handler (and, via Pc, that
        // RTI correctly resumed and completed the interrupted main flow back at $810),
        // not just that state.Interrupt_Hit got set internally (the previous test checks
        // that in isolation).
        emulator.AssertState(A: 0xab, Pc: 0x813);
        Assert.IsFalse(emulator.Interrupt);
        Assert.IsTrue((emulator.State.Interrupt_Hit & (uint)InterruptSource.UartRda) == 0);
    }

    // --- uart_dlm_ier_write and uart_fcr_write both only store the value the CPU
    // wrote -- neither originally re-checked the current FIFO count against
    // fifo_trigger and raised the interrupt if the condition was already satisfied.
    // Per the TL16C2550 datasheet, the trigger comparison and IER gating live in the
    // bus-clock-speed register logic, not the baud-rate-clock serial logic, so there's
    // no reason either write shouldn't take effect immediately if the underlying
    // condition already holds. uart_fcr_write's recheck has since been implemented
    // (see the test below); uart_dlm_ier_write's is still a "todo" and the
    // corresponding test is expected to FAIL.

    [TestMethod]
    public async Task Inbound_RdaInterrupt_AssertsImmediatelyWhenEnabledAfterDataAlreadyPresent()
    {
        var mock = new MockZiModemHost();
        var emulator = new Emulator(new EmulatorOptions { ZiModemHostOverride = mock.Exports });
        emulator.Uart.FifoEnabled = true; // FCR bit 0 -- required for FIFO reads/writes

        // A byte arrives with IER still disabled. Trigger level defaults to 0 (FCR never
        // written), so the trigger condition is already satisfied -- IIR's RDA bit should
        // already be pending, but interrupt_hit must not be (IER was off at arrival time).
        // SEI first -- this test checks state.Interrupt_Hit directly, it isn't exercising
        // real IRQ vectoring (there's no vector table set up here, see
        // Inbound_RdaInterrupt_VectorsToHandler for that), and once fixed this test will
        // make the interrupt condition go live for real, so the CPU must never actually
        // act on the pending IRQ. The I flag persists across Emulate() calls on the same
        // Emulator, so setting it here covers the second stage below too.
        mock.EnqueueInbound(0x41);

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sei
                nop
                nop
                stp",
                emulator);

        emulator.AssertState(Pc: 0x814);
        Assert.IsFalse(emulator.Uart.EmptyInbound);
        Assert.AreNotEqual(0, emulator.Memory[0x9fe2] & 0b0001); // IIR: RDA already pending
        Assert.IsTrue((emulator.State.Interrupt_Hit & (uint)InterruptSource.UartRda) == 0); // not raised -- IER was off

        // Now enable RDA with the data still sitting there and nothing new arriving.
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #%00000001
                sta $9fe1      ; IER: enable RDA
                stp",
                emulator);

        emulator.AssertState(Pc: 0x816);
        Assert.IsTrue(emulator.Uart.InterruptRdaEnabled);
        Assert.IsTrue((emulator.State.Interrupt_Hit & (uint)InterruptSource.UartRda) != 0,
            "enabling RDA should immediately assert the interrupt since the trigger condition was already satisfied");
    }

    [TestMethod]
    public async Task Inbound_RdaInterrupt_AssertsImmediatelyWhenTriggerLoweredBelowCurrentCount()
    {
        var mock = new MockZiModemHost();
        var emulator = new Emulator(new EmulatorOptions { ZiModemHostOverride = mock.Exports });
        emulator.Uart.FifoEnabled = true; // FCR bit 0 -- required for FIFO reads/writes

        // Stage 1: enable RDA and set a high trigger level (8) with nothing queued yet,
        // so it's harmless for the free tick to spend itself here. SEI first -- same
        // reasoning as the previous test: this checks state.Interrupt_Hit directly, and
        // once fixed the final stage below makes the interrupt go live for real, so the
        // CPU must never actually act on it (no vector table here). The I flag persists
        // across Emulate() calls on the same Emulator, so this covers every later stage.
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sei
                lda #%00000001
                sta $9fe1      ; IER: enable RDA
                lda #%10000000
                sta $9fe2      ; FCR: trigger level 8
                stp",
                emulator);

        emulator.AssertState(Pc: 0x81C);
        Assert.IsTrue(emulator.Uart.InterruptRdaEnabled);
        Assert.AreEqual(8u, emulator.Uart.FifoTrigger);

        // Force fast ticks (see the other tests in this file for why both lines are
        // needed), then deliver 4 bytes -- below the trigger level of 8, so the
        // interrupt must not fire yet even though it's enabled.
        emulator.Uart.CpuTicks = 1;
        emulator.ClockUart = 0;
        mock.EnqueueInbound(new byte[] { 0x01, 0x02, 0x03, 0x04 });

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                nop
                nop
                nop
                nop
                nop
                nop
                nop
                nop
                stp",
                emulator);

        emulator.AssertState(Pc: 0x819);
        Assert.AreEqual(0, emulator.Memory[0x9fe2] & 0b0001,
            "IIR RDA must not be pending yet -- only 4 of the required 8 bytes have arrived");
        Assert.IsTrue((emulator.State.Interrupt_Hit & (uint)InterruptSource.UartRda) == 0);

        // Now lower the trigger to 1 with those 4 bytes still sitting there and nothing
        // new arriving.
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #%00000000
                sta $9fe2      ; FCR: trigger level 1
                stp",
                emulator);

        emulator.AssertState(Pc: 0x816);
        Assert.AreEqual(1u, emulator.Uart.FifoTrigger);
        Assert.IsTrue((emulator.State.Interrupt_Hit & (uint)InterruptSource.UartRda) != 0,
            "lowering the trigger below the already-queued count should immediately assert the interrupt");
    }

    [TestMethod]
    public async Task Fcr_TriggerSatisfiedWhileInterruptsDisabled_SetsIirButNotInterruptHit()
    {
        var mock = new MockZiModemHost();
        var emulator = new Emulator(new EmulatorOptions { ZiModemHostOverride = mock.Exports });
        emulator.Uart.FifoEnabled = true; // FCR bit 0 -- required for FIFO reads/writes

        // Stage 1: IER RDA is never enabled anywhere in this test. Set a high trigger
        // (8) with nothing queued yet, so it's harmless for the free tick to spend
        // itself here.
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #%10000000
                sta $9fe2      ; FCR: trigger level 8
                stp",
                emulator);

        emulator.AssertState(Pc: 0x816);
        Assert.AreEqual(8u, emulator.Uart.FifoTrigger);

        // Force fast ticks and deliver 2 bytes -- below the trigger of 8, so IIR must
        // not be pending yet. This exercises uart_tick's own trigger check as the
        // bytes arrive, not the FCR recheck below.
        emulator.Uart.CpuTicks = 1;
        emulator.ClockUart = 0;
        mock.EnqueueInbound(new byte[] { 0x01, 0x02 });

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                nop
                nop
                stp",
                emulator);

        emulator.AssertState(Pc: 0x813);
        Assert.AreEqual(0, emulator.Memory[0x9fe2] & 0b0001,
            "IIR RDA must not be pending yet -- only 2 of the required 8 bytes have arrived");

        // Now lower the trigger to 1 with those 2 bytes still sitting there and
        // nothing new arriving -- the condition becomes satisfied, but RDA was never
        // enabled in IER, so the interrupt itself must never fire even though IIR
        // reports the condition as pending.
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #%00000000
                sta $9fe2      ; FCR: trigger level 1
                stp",
                emulator);

        emulator.AssertState(Pc: 0x816);
        Assert.AreEqual(1u, emulator.Uart.FifoTrigger);
        Assert.IsFalse(emulator.Uart.InterruptRdaEnabled);
        Assert.AreNotEqual(0, emulator.Memory[0x9fe2] & 0b0001,
            "IIR RDA should now be pending -- the trigger condition is satisfied");
        Assert.IsTrue((emulator.State.Interrupt_Hit & (uint)InterruptSource.UartRda) == 0,
            "interrupt must not fire -- RDA was never enabled in IER");
    }

    [TestMethod]
    public async Task Fcr_TriggerRaisedAboveCurrentCount_ClearsIirAndInterruptHit()
    {
        var mock = new MockZiModemHost();
        var emulator = new Emulator(new EmulatorOptions { ZiModemHostOverride = mock.Exports });
        emulator.Uart.FifoEnabled = true; // FCR bit 0 -- required for FIFO reads/writes

        // Stage 1: enable RDA and set a low trigger level (1) with nothing queued yet,
        // so it's harmless for the free tick to spend itself here. SEI first -- this
        // test checks state.Interrupt_Hit directly and expects it to go live for real
        // once data arrives below, so the CPU must never actually act on the pending
        // IRQ (no vector table here). The I flag persists across Emulate() calls on
        // the same Emulator, so this covers every later stage.
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sei
                lda #%00000001
                sta $9fe1      ; IER: enable RDA
                lda #%00000000
                sta $9fe2      ; FCR: trigger level 1
                stp",
                emulator);

        emulator.AssertState(Pc: 0x81C);
        Assert.IsTrue(emulator.Uart.InterruptRdaEnabled);
        Assert.AreEqual(1u, emulator.Uart.FifoTrigger);

        // Force fast ticks and deliver 4 bytes -- comfortably over the trigger of 1,
        // so uart_tick's own check raises the interrupt as they arrive.
        emulator.Uart.CpuTicks = 1;
        emulator.ClockUart = 0;
        mock.EnqueueInbound(new byte[] { 0x01, 0x02, 0x03, 0x04 });

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                nop
                nop
                nop
                nop
                nop
                nop
                nop
                nop
                stp",
                emulator);

        emulator.AssertState(Pc: 0x819);
        Assert.AreNotEqual(0, emulator.Memory[0x9fe2] & 0b0001, "IIR RDA should be pending -- 4 bytes queued, trigger is 1");
        Assert.IsTrue((emulator.State.Interrupt_Hit & (uint)InterruptSource.UartRda) != 0, "interrupt should already be live");

        // Now raise the trigger to 8 with those same 4 bytes still sitting there and
        // nothing new arriving -- the condition is no longer satisfied, so both the
        // IIR bit and the live interrupt must clear.
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #%10000000
                sta $9fe2      ; FCR: trigger level 8
                stp",
                emulator);

        emulator.AssertState(Pc: 0x816);
        Assert.AreEqual(8u, emulator.Uart.FifoTrigger);
        Assert.AreEqual(0, emulator.Memory[0x9fe2] & 0b0001,
            "IIR RDA should no longer be pending -- only 4 of the required 8 bytes remain queued");
        Assert.IsTrue((emulator.State.Interrupt_Hit & (uint)InterruptSource.UartRda) == 0,
            "raising the trigger above the already-queued count should clear the interrupt");
    }

    // --- FCR bits 1/2: "Clear Receiver FIFO" and "Clear Transmitter FIFO". Per the
    // TL16C2550 datasheet, writing FCR with these bits set resets the corresponding
    // FIFO's read/write pointers, discards anything still queued, and clears the
    // associated LSR/IIR/interrupt state (RX here: DR + IIR_RDA + interrupt_hit's RDA
    // flag; TX is in OutboundBuffer.cs -- see Fcr_ClearTransmitterFifoBit_ResetsOutboundFifo).
    // uart_fcr_write currently only ever extracts the trigger-level bits (6-7); it never
    // looks at bits 1/2 at all, so this test is expected to FAIL until that's implemented.

    [TestMethod]
    public async Task Fcr_ClearReceiverFifoBit_ResetsInboundFifo()
    {
        var mock = new MockZiModemHost();
        var emulator = new Emulator(new EmulatorOptions { ZiModemHostOverride = mock.Exports });
        emulator.Uart.FifoEnabled = true; // FCR bit 0 -- required for FIFO reads/writes

        // Stage 1: enable RDA and queue 3 bytes -- comfortably over the default trigger
        // (0, so any count >= 1 satisfies it) -- so DR, IIR RDA and interrupt_hit are all
        // live by the time we clear the FIFO. SEI first -- same reasoning as the other
        // interrupt tests in this file: checks state.Interrupt_Hit directly rather than
        // real vectoring, so the CPU must never actually act on the pending IRQ.
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sei
                lda #%00000001
                sta $9fe1      ; IER: enable RDA
                stp",
                emulator);

        emulator.AssertState(Pc: 0x817);

        emulator.Uart.CpuTicks = 1;
        emulator.ClockUart = 0;
        mock.EnqueueInbound(new byte[] { 0x01, 0x02, 0x03 });

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                nop
                nop
                nop
                stp",
                emulator);

        emulator.AssertState(Pc: 0x814);
        Assert.IsFalse(emulator.Uart.EmptyInbound, "bytes should have arrived");
        Assert.AreNotEqual(0, emulator.Memory[0x9fe5] & 0b00000001, "DR should be set");
        Assert.AreNotEqual(0, emulator.Memory[0x9fe2] & 0b0001, "IIR RDA should be pending");
        Assert.IsTrue((emulator.State.Interrupt_Hit & (uint)InterruptSource.UartRda) != 0, "interrupt should be live");

        // Stage 2: clear the RX FIFO via FCR bit 1. Nothing new arrives in between.
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #%00000010
                sta $9fe2      ; FCR: clear receiver FIFO
                stp",
                emulator);

        emulator.AssertState(Pc: 0x816);
        Assert.IsTrue(emulator.Uart.EmptyInbound, "the RX FIFO should be reset to empty");
        Assert.AreEqual(emulator.Uart.WriteIndexInbound, emulator.Uart.ReadIndexInbound,
            "read/write pointers should both reset together");
        Assert.AreEqual(0, emulator.Memory[0x9fe5] & 0b00000001, "DR should clear -- the FIFO is now empty");
        Assert.AreEqual(0, emulator.Memory[0x9fe2] & 0b0001, "IIR RDA should clear -- nothing left queued");
        Assert.IsTrue((emulator.State.Interrupt_Hit & (uint)InterruptSource.UartRda) == 0,
            "clearing the RX FIFO should also clear the live RDA interrupt");
    }
}
