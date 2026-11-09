using Microsoft.VisualStudio.TestTools.UnitTesting;
using static BitMagic.X16Emulator.Tests.SdCard.SdSpi;

namespace BitMagic.X16Emulator.Tests.SdCard;

// Multiple block reads (CMD18) and stopping them (CMD12), issue #216. Checked on a real X16: blocks follow one after
// another, each with an $FE token (hardware sends a few $FF before each, which the emulator leaves out). CMD12 mid read
// gives a stuff byte then R1 $00, and with nothing being read it's accepted with R1 $00.
[TestClass]
public class SdCardMultiBlock
{
    private static readonly byte[] Stop = Command(12, 0);

    // CMD18 at 0 to 5, R1 at 6, then each block is a token, 512 bytes and 2 CRC bytes (515), from 7
    private static int Token(int block) => 7 + block * 515;

    [TestMethod]
    public async Task ThreeBlocksThenStop()
    {
        using var card = MakeCard(out var blocks);

        // three blocks, then block 3's token and 15 bytes, then CMD12, then 8 more
        var clocked = 1 + 3 * 515 + 16;
        var capture = await Run(card, Send(Command(18, 0)), Clock(clocked), Send(Stop), Clock(8));

        Assert.AreEqual(0x00, capture[6], "R1");
        for (var i = 0; i < 3; i++)
        {
            Assert.AreEqual(0xfe, capture[Token(i)], $"block {i}'s token");
            CollectionAssert.AreEqual(blocks[i], capture[(Token(i) + 1)..(Token(i) + 513)], $"block {i}");
        }

        // block 3 carries on while CMD12 arrives, then the stuff byte and R1, then nothing
        var stop = 6 + clocked;
        Assert.AreEqual(0xfe, capture[Token(3)], "block 3's token");
        CollectionAssert.AreEqual(blocks[3][..21], capture[(Token(3) + 1)..(stop + 6)], "Block 3 carries on while CMD12 arrives.");
        Assert.AreEqual(0xff, capture[stop + 6], "stuff byte");
        Assert.AreEqual(0x00, capture[stop + 7], "R1");
        Assert.IsTrue(capture[(stop + 8)..(stop + 14)].All(i => i == 0xff), $"The read stops: {Hex(capture[stop..(stop + 14)])}");
    }

    // from the last block, then the end of the card: an error token, out of range
    [TestMethod]
    public async Task PastTheEnd_ErrorToken()
    {
        using var card = MakeCard(out _);
        var last = (uint)(card.Size / 512 - 1);
        var pattern = Pattern(9);
        pattern.CopyTo(card.Image[(int)(last * 512)..]);

        var capture = await Run(card, Send(Command(18, last)), Clock(1 + 515 + 8));

        Assert.AreEqual(0x00, capture[6], "R1");
        Assert.AreEqual(0xfe, capture[7], "token");
        CollectionAssert.AreEqual(pattern, capture[8..520], "The last block.");
        Assert.AreEqual(0x08, capture[Token(1)], "error token, out of range");
        Assert.IsTrue(capture[(Token(1) + 1)..(Token(1) + 8)].All(i => i == 0xff), $"The read ends: {Hex(capture[Token(1)..(Token(1) + 8)])}");
    }

    // starting past the end, nothing comes back, as for CMD17
    [TestMethod]
    public async Task StartPastTheEnd_NoReply()
    {
        using var card = MakeCard(out _);

        var capture = await Run(card, Send(Command(18, (uint)(card.Size / 512))), Clock(16));

        Assert.IsTrue(capture[6..22].All(i => i == 0xff), Hex(capture[..22]));
    }

    // another command during the read is rejected, and the read ends
    [TestMethod]
    public async Task OtherCommand_Rejected()
    {
        using var card = MakeCard(out var blocks);

        var capture = await Run(card, Send(Command(18, 0)), Clock(18), Send(Command(17, 1)), Clock(32));

        CollectionAssert.AreEqual(blocks[0][..22], capture[8..30], "Block 0 carries on while the command arrives.");
        Assert.AreEqual(0xff, capture[30], "stuff byte");
        Assert.AreEqual(0x04, capture[31], "R1, illegal command");
        Assert.IsTrue(capture[32..62].All(i => i == 0xff), $"The read ends: {Hex(capture[30..62])}");
    }

    // with nothing being read, CMD12 is accepted
    [TestMethod]
    public async Task Cmd12_Idle()
    {
        using var card = MakeCard(out _);

        var capture = await Run(card, Send(Stop), Clock(4));

        Assert.AreEqual(0x00, capture[6], "R1");
        Assert.IsTrue(capture[7..10].All(i => i == 0xff), Hex(capture[..10]));
    }

    // after a stopped CMD18, a normal read works
    [TestMethod]
    public async Task AfterStop_ReadWorks()
    {
        using var card = MakeCard(out var blocks);

        // CMD18 and 16 bytes (to 23), CMD12 (24 to 29) and 8 bytes of its reply (to 37), CMD17 at 38 to 43
        var capture = await Run(card, Send(Command(18, 0)), Clock(18), Send(Stop), Clock(8), Send(Command(17, 2)), Clock(520));

        Assert.AreEqual(0x00, capture[44], "R1");
        Assert.AreEqual(0xfe, capture[45], "token");
        CollectionAssert.AreEqual(blocks[2], capture[46..558]);
    }

    // With auto transmit, reading SPI_DATA clocks out the next byte, so the next block has to follow that way too. The
    // first read gives back the byte already there (the R1), then the token and data follow.
    [TestMethod]
    public async Task AutoTransmit_AcrossBlocks()
    {
        using var card = MakeCard(out var blocks);

        // CMD18 at 0 to 5, R1 at 6, then auto reads from 7: the R1 again, then block 0's token at 8
        var capture = await Run(card, Send(Command(18, 0)), Clock(1), AutoTx, AutoRead(2 * 515 + 1));

        Assert.AreEqual(0x00, capture[6], "R1");
        Assert.AreEqual(0x00, capture[7], "the first read gives back the R1");
        Assert.AreEqual(0xfe, capture[8], "block 0's token");
        CollectionAssert.AreEqual(blocks[0], capture[9..521], "block 0");
        Assert.AreEqual(0xfe, capture[523], "block 1's token");
        CollectionAssert.AreEqual(blocks[1], capture[524..1036], "block 1");
    }
}
