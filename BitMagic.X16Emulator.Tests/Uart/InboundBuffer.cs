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

        mock.EnqueueInbound(0x41);

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                nop
                nop
                stp",
                emulator);

        Assert.AreEqual((byte)0x41, emulator.Memory[0x9fe0]);
        Assert.AreNotEqual(0, emulator.Memory[0x9fe5] & 0b00000001); // DR
        Assert.IsFalse(emulator.Uart.EmptyInbound);
    }

    [TestMethod]
    public async Task Inbound_Read_ClearsDrOnceDrained()
    {
        var mock = new MockZiModemHost();
        var emulator = new Emulator(new EmulatorOptions { ZiModemHostOverride = mock.Exports });

        mock.EnqueueInbound(0x41);

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                nop
                nop
                lda $9fe0
                stp",
                emulator);

        emulator.AssertState(A: 0x41); // the LDA actually read the delivered byte, not a stale 0
        Assert.AreEqual(0, emulator.Memory[0x9fe5] & 0b00000001); // DR cleared
        Assert.IsTrue(emulator.Uart.EmptyInbound);
    }

    [TestMethod]
    public async Task Inbound_RdaInterrupt_FiresOnceEnabledAndClearsOnceDrained()
    {
        var mock = new MockZiModemHost();
        var emulator = new Emulator(new EmulatorOptions { ZiModemHostOverride = mock.Exports });

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

        Assert.IsTrue((emulator.State.Interrupt_Hit & (uint)InterruptSource.UartRda) != 0);

        // Draining the only queued byte should drop the count back below the trigger,
        // clearing the interrupt again.
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda $9fe0
                stp",
                emulator);

        Assert.IsTrue((emulator.State.Interrupt_Hit & (uint)InterruptSource.UartRda) == 0);
    }

    [TestMethod]
    public async Task Inbound_RdaInterrupt_VectorsToHandler()
    {
        var mock = new MockZiModemHost();
        var emulator = new Emulator(new EmulatorOptions { ZiModemHostOverride = mock.Exports });

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

        // Proves the CPU actually vectored to $900 and ran the handler, not just that
        // state.Interrupt_Hit got set internally (that's what the previous test checks).
        emulator.AssertState(0xab);
        Assert.IsFalse(emulator.Interrupt);
        Assert.IsTrue((emulator.State.Interrupt_Hit & (uint)InterruptSource.UartRda) == 0);
    }
}
