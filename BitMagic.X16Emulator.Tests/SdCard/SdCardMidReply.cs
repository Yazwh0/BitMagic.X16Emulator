using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.SdCard;

// A command sent while the card is still sending a reply, eg part way through a block from CMD17. Checked on a real
// X16 (issue #195), matching SD spec 7.2.8: the card keeps sending the block while the command arrives, then sends a
// stuff byte and an R1. CMD12 is accepted (R1 $00, then busy). Anything else is rejected (R1 $04) and not carried out.
[TestClass]
public class SdCardMidReply
{
    private const int Capture = 0x4000;
    private const int CaptureLength = 0x500;

    private static readonly byte[] Read0 = Command(17, 0);
    private static readonly byte[] Read1 = Command(17, 1);
    private static readonly byte[] Stop = Command(12, 0);

    private static byte[] Command(int command, uint block) =>
        [(byte)(0x40 | command), (byte)(block >> 24), (byte)(block >> 16), (byte)(block >> 8), (byte)block, 0x01];

    // a step: send these bytes, keeping what comes back
    private static string Send(IEnumerable<byte> bytes) =>
        string.Concat(bytes.Select(b => $"lda #${b:X2}\n jsr exchange\n"));

    // a step: clock out this many bytes with $FF, keeping what comes back
    private static string Clock(int count) => $@"
            lda #<{count}
            sta $24
            lda #>{count}
            sta $25
            jsr clock";

    private static async Task<byte[]> Run(X16Emulator.SdCard card, params string[] steps)
    {
        using var emulator = new Emulator();
        emulator.LoadSdCard(card);

        await X16TestHelper.Emulate($@"
                .machine CommanderX16R40
                .org $810
                    sei
                    lda #<${Capture:X4}
                    sta $22
                    lda #>${Capture:X4}
                    sta $23
                    lda #1
                    sta SPI_CTRL    ; select the card
                    {string.Join("\n", steps)}
                    stp

                ; sends A, stores what came back at ($22) and moves it on
                .proc exchange
                    sta SPI_DATA
                    lda SPI_DATA
                    ldy #0
                    sta ($22), y
                    inc $22
                    bne done
                    inc $23
                .done:
                    rts
                .endproc

                ; exchanges $FF ($24/$25) times
                .proc clock
                .loop:
                    lda #$ff
                    jsr exchange
                    lda $24
                    bne low
                    dec $25
                .low:
                    dec $24
                    lda $24
                    ora $25
                    bne loop
                    rts
                .endproc",
                emulator);

        return emulator.Memory.Slice(Capture, CaptureLength).ToArray();
    }

    private static X16Emulator.SdCard MakeCard(out byte[] block0, out byte[] block1)
    {
        var card = new X16Emulator.SdCard(16, new NullLogger());
        block0 = Enumerable.Range(0, 512).Select(i => (byte)(i * 3 + 1)).ToArray();
        block1 = Enumerable.Range(0, 512).Select(i => (byte)(i * 5 + 2)).ToArray();
        block0.CopyTo(card.Image);
        block1.CopyTo(card.Image[512..]);
        return card;
    }

    private static string Hex(IEnumerable<byte> bytes) => string.Join(" ", bytes.Select(i => i.ToString("X2")));

    // The capture: 0 to 5 is CMD17 going out (the card sends $FF), 6 is R1 ($00), 7 the $FE token, then 16 bytes of
    // block 0 at 8 to 23. The command mid block goes out at 24 to 29, so its stuff byte is at 30 and its R1 at 31.
    private static void AssertStartAndDataDuringCommand(byte[] capture, byte[] block0)
    {
        Assert.AreEqual(0x00, capture[6], "R1");
        Assert.AreEqual(0xfe, capture[7], "token");
        CollectionAssert.AreEqual(block0[..22], capture[8..30], $"Block 0 should carry on while the command arrives: {Hex(capture[..40])}");
    }

    [TestMethod]
    public async Task Cmd17MidBlock_Rejected()
    {
        using var card = MakeCard(out var block0, out _);

        var capture = await Run(card, Send(Read0), Clock(18), Send(Read1), Clock(32));

        AssertStartAndDataDuringCommand(capture, block0);
        Assert.AreEqual(0xff, capture[30], "stuff byte");
        Assert.AreEqual(0x04, capture[31], "R1, illegal command");
        Assert.IsTrue(capture[32..62].All(i => i == 0xff), $"Nothing after the R1, block 1 isn't read: {Hex(capture[30..62])}");
    }

    [TestMethod]
    public async Task Cmd12MidBlock_Stops()
    {
        using var card = MakeCard(out var block0, out _);

        var capture = await Run(card, Send(Read0), Clock(18), Send(Stop), Clock(32));

        AssertStartAndDataDuringCommand(capture, block0);
        Assert.AreEqual(0xff, capture[30], "stuff byte");
        Assert.AreEqual(0x00, capture[31], "R1, accepted");
        Assert.AreEqual(0x00, capture[32], "busy");
        Assert.IsTrue(capture[33..62].All(i => i == 0xff), $"The block stops: {Hex(capture[30..62])}");
    }

    // after either, the card works as normal
    [TestMethod]
    [DataRow(17)]
    [DataRow(12)]
    public async Task AfterMidBlockCommand_ReadWorks(int command)
    {
        using var card = MakeCard(out _, out var block1);
        var mid = command == 12 ? Stop : Read1;

        // CMD17 and 16 bytes, the command and 8 bytes of its reply (to 37), then a normal CMD17 for block 1 at 38 to 43
        var capture = await Run(card, Send(Read0), Clock(18), Send(mid), Clock(8), Send(Read1), Clock(520));

        Assert.AreEqual(0x00, capture[44], "R1");
        Assert.AreEqual(0xfe, capture[45], "token");
        CollectionAssert.AreEqual(block1, capture[46..558]);
    }

    // Only a read is interrupted. CMD8's reply is 5 bytes: reading just the R1, then sending CMD17, still reads.
    [TestMethod]
    public async Task ShortReplyNotAllRead_NextCommandCarriedOut()
    {
        using var card = MakeCard(out var block0, out _);
        byte[] cmd8 = [0x48, 0x00, 0x00, 0x01, 0xaa, 0x87];

        // CMD8 at 0 to 5, its R1 at 6, then CMD17 at 7 to 12
        var capture = await Run(card, Send(cmd8), Clock(1), Send(Read0), Clock(520));

        Assert.AreEqual(0x01, capture[6], "CMD8's R1");
        Assert.AreEqual(0x00, capture[13], "R1");
        Assert.AreEqual(0xfe, capture[14], "token");
        CollectionAssert.AreEqual(block0, capture[15..527]);
    }

    // a command whose last byte is clocked with the block's last CRC byte comes after the read, so it's carried out
    [TestMethod]
    public async Task CommandAsTheReplyEnds_CarriedOut()
    {
        using var card = MakeCard(out _, out var block1);

        // the reply is 516 bytes (R1, token, 512, CRC x2) at 6 to 521: 510 clocked, then the command's 6 take the rest
        var capture = await Run(card, Send(Read0), Clock(510), Send(Read1), Clock(520));

        Assert.AreEqual(0x00, capture[522], "R1");
        Assert.AreEqual(0xfe, capture[523], "token");
        CollectionAssert.AreEqual(block1, capture[524..1036], "Block 1 should follow.");
    }
}
