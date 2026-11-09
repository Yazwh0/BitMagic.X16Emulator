using Microsoft.VisualStudio.TestTools.UnitTesting;
using static BitMagic.X16Emulator.Tests.SdCard.SdSpi;

namespace BitMagic.X16Emulator.Tests.SdCard;

// A command sent while the card is still sending a reply, eg part way through a block from CMD17. Checked on a real
// X16 (issues #195 and #216), matching SD spec 7.2.8: the card keeps sending the block while the command arrives, then
// sends a stuff byte and an R1. CMD12 is accepted (R1 $00, no busy on the card tested). Anything else is rejected
// (R1 $04) and not carried out.
[TestClass]
public class SdCardMidReply
{
    private static readonly byte[] Read0 = Command(17, 0);
    private static readonly byte[] Read1 = Command(17, 1);
    private static readonly byte[] Stop = Command(12, 0);

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
        using var card = MakeCard(out var blocks);

        var capture = await Run(card, Send(Read0), Clock(18), Send(Read1), Clock(32));

        AssertStartAndDataDuringCommand(capture, blocks[0]);
        Assert.AreEqual(0xff, capture[30], "stuff byte");
        Assert.AreEqual(0x04, capture[31], "R1, illegal command");
        Assert.IsTrue(capture[32..62].All(i => i == 0xff), $"Nothing after the R1, block 1 isn't read: {Hex(capture[30..62])}");
    }

    [TestMethod]
    public async Task Cmd12MidBlock_Stops()
    {
        using var card = MakeCard(out var blocks);

        var capture = await Run(card, Send(Read0), Clock(18), Send(Stop), Clock(32));

        AssertStartAndDataDuringCommand(capture, blocks[0]);
        Assert.AreEqual(0xff, capture[30], "stuff byte");
        Assert.AreEqual(0x00, capture[31], "R1, accepted");
        Assert.IsTrue(capture[32..62].All(i => i == 0xff), $"The block stops, with no busy: {Hex(capture[30..62])}");
    }

    // after either, the card works as normal
    [TestMethod]
    [DataRow(17)]
    [DataRow(12)]
    public async Task AfterMidBlockCommand_ReadWorks(int command)
    {
        using var card = MakeCard(out var blocks);
        var mid = command == 12 ? Stop : Read1;

        // CMD17 and 16 bytes, the command and 8 bytes of its reply (to 37), then a normal CMD17 for block 1 at 38 to 43
        var capture = await Run(card, Send(Read0), Clock(18), Send(mid), Clock(8), Send(Read1), Clock(520));

        Assert.AreEqual(0x00, capture[44], "R1");
        Assert.AreEqual(0xfe, capture[45], "token");
        CollectionAssert.AreEqual(blocks[1], capture[46..558]);
    }

    // Only a read is interrupted. CMD8's reply is 5 bytes: reading just the R1, then sending CMD17, still reads.
    [TestMethod]
    public async Task ShortReplyNotAllRead_NextCommandCarriedOut()
    {
        using var card = MakeCard(out var blocks);
        byte[] cmd8 = [0x48, 0x00, 0x00, 0x01, 0xaa, 0x87];

        // CMD8 at 0 to 5, its R1 at 6, then CMD17 at 7 to 12
        var capture = await Run(card, Send(cmd8), Clock(1), Send(Read0), Clock(520));

        Assert.AreEqual(0x01, capture[6], "CMD8's R1");
        Assert.AreEqual(0x00, capture[13], "R1");
        Assert.AreEqual(0xfe, capture[14], "token");
        CollectionAssert.AreEqual(blocks[0], capture[15..527]);
    }

    // a command whose last byte is clocked with the block's last CRC byte comes after the read, so it's carried out
    [TestMethod]
    public async Task CommandAsTheReplyEnds_CarriedOut()
    {
        using var card = MakeCard(out var blocks);

        // the reply is 516 bytes (R1, token, 512, CRC x2) at 6 to 521: 510 clocked, then the command's 6 take the rest
        var capture = await Run(card, Send(Read0), Clock(510), Send(Read1), Clock(520));

        Assert.AreEqual(0x00, capture[522], "R1");
        Assert.AreEqual(0xfe, capture[523], "token");
        CollectionAssert.AreEqual(blocks[1], capture[524..1036], "Block 1 should follow.");
    }
}
