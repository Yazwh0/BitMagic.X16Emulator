using BitMagic.X16Emulator.TestHelper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Uart;

[TestClass]
public class OutboundBuffer
{
    private const byte LsrEmptyMask = 0b01100000; // THRE | TEMT, as defined by LSR_Empty in Uart.asm

    // Writes `count` bytes (values 0x00, 0x01, 0x02, ...) to $9fe0 in sequence.
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

        CollectionAssert.AreEqual(new byte[] { 0x01, 0x02, 0x03 }, mock.SentBytes.ToArray());
        Assert.IsTrue(emulator.Uart.EmptyOutbound);
    }
}
