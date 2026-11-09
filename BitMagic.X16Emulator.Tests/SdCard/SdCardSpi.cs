using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;

namespace BitMagic.X16Emulator.Tests.SdCard;

// Reads and writes SD card blocks over SPI from 6502 code, as the ROM does: CMD17 (read) and CMD24 (write), with the
// block number as the argument.
[TestClass]
public class SdCardSpi
{
    private const int Reply = 0x4000;       // where the reply bytes are stored, three pages
    private const int Pattern = 0x5000;     // the 512 bytes to write
    private const int ReplyLength = 0x300;

    private static string Command(int command, uint block) =>
        string.Concat(new[] { 0x40 | command, (int)(block >> 24) & 0xff, (int)(block >> 16) & 0xff, (int)(block >> 8) & 0xff, (int)block & 0xff, 0x01 }
            .Select(b => $"lda #${b:X2}\n sta SPI_DATA\n"));

    // clocks out ReplyLength bytes into Reply
    private static string ReadReply(string name) => $@"
            ldy #0
        .{name}_0:
            lda #$ff
            sta SPI_DATA
            lda SPI_DATA
            sta ${Reply:X4}, y
            iny
            bne {name}_0
        .{name}_1:
            lda #$ff
            sta SPI_DATA
            lda SPI_DATA
            sta ${Reply + 0x100:X4}, y
            iny
            bne {name}_1
        .{name}_2:
            lda #$ff
            sta SPI_DATA
            lda SPI_DATA
            sta ${Reply + 0x200:X4}, y
            iny
            bne {name}_2";

    // CMD24, then the R1 reply, then the data packet: $FE, the 512 bytes at Pattern, and two CRC bytes
    private static string Write(uint block) => $@"
            {Command(24, block)}
            lda #$ff
            sta SPI_DATA
            lda SPI_DATA
            sta ${Reply:X4}

            lda #$fe
            sta SPI_DATA
            ldy #0
        .write_0:
            lda ${Pattern:X4}, y
            sta SPI_DATA
            iny
            bne write_0
        .write_1:
            lda ${Pattern + 0x100:X4}, y
            sta SPI_DATA
            iny
            bne write_1
            stz SPI_DATA
            stz SPI_DATA";

    private static string Read(uint block) => $@"
            {Command(17, block)}
            {ReadReply("read")}";

    private static async Task Run(Emulator emulator, string body)
    {
        await X16TestHelper.Emulate($@"
                .machine CommanderX16R40
                .org $810
                    sei
                    lda #1
                    sta SPI_CTRL    ; select the card
                    {body}
                    stp",
                emulator);
    }

    private static byte[] MakePattern(int seed) => Enumerable.Range(0, 512).Select(i => (byte)(i * 7 + seed)).ToArray();

    private static byte[] ReplyBytes(Emulator emulator) => emulator.Memory.Slice(Reply, ReplyLength).ToArray();

    // the reply to CMD17 is R1 ($00), the $FE start token, then the block
    private static byte[]? BlockFromReply(byte[] reply)
    {
        var token = Array.IndexOf(reply, (byte)0xfe);
        if (token < 0 || token + 1 + 512 > reply.Length)
            return null;

        Assert.AreEqual(0x00, reply[token - 1], "R1");
        return reply[(token + 1)..(token + 1 + 512)];
    }

    // the 512 byte VHD footer straight after the card's data, which isn't part of the card
    private static unsafe Span<byte> Footer(X16Emulator.SdCard card) => new((void*)(card.MemoryPtr + card.Size), 512);

    [TestMethod]
    public async Task Read_LastBlock()
    {
        using var card = new X16Emulator.SdCard(16, new NullLogger());
        using var emulator = new Emulator();
        emulator.LoadSdCard(card);

        var lastBlock = (uint)(card.Size / 512 - 1);
        var pattern = MakePattern(3);
        pattern.CopyTo(card.Image[(int)(lastBlock * 512)..]);

        await Run(emulator, Read(lastBlock));

        CollectionAssert.AreEqual(pattern, BlockFromReply(ReplyBytes(emulator)));
    }

    [TestMethod]
    public async Task Read_PastTheEnd()
    {
        using var card = new X16Emulator.SdCard(16, new NullLogger());
        using var emulator = new Emulator();
        emulator.LoadSdCard(card);

        Assert.AreEqual("conectix", Encoding.ASCII.GetString(Footer(card)[..8]), "The VHD footer should follow the card.");

        await Run(emulator, Read((uint)(card.Size / 512)));

        Assert.IsNull(BlockFromReply(ReplyBytes(emulator)), "A block past the end shouldn't be sent, it's the VHD footer.");
    }

    [TestMethod]
    public async Task Write_PastTheEnd()
    {
        using var card = new X16Emulator.SdCard(16, new NullLogger());
        using var emulator = new Emulator();
        emulator.LoadSdCard(card);

        var footer = Footer(card).ToArray();
        MakePattern(5).CopyTo(emulator.Memory[Pattern..]);

        await Run(emulator, Write((uint)(card.Size / 512)));

        CollectionAssert.AreEqual(footer, Footer(card).ToArray(), "Writing past the end shouldn't change the VHD footer.");
    }

    [TestMethod]
    public async Task Write_LastBlock()
    {
        using var card = new X16Emulator.SdCard(16, new NullLogger());
        using var emulator = new Emulator();
        emulator.LoadSdCard(card);

        var lastBlock = (uint)(card.Size / 512 - 1);
        var pattern = MakePattern(9);
        pattern.CopyTo(emulator.Memory[Pattern..]);

        await Run(emulator, Write(lastBlock));

        CollectionAssert.AreEqual(pattern, card.Image.Slice((int)(lastBlock * 512), 512).ToArray());
    }

    // a block above 4GB, which needs more than 32 bits as a byte offset, written then read back
    [TestMethod]
    public async Task WriteAndRead_Above4GB()
    {
        using var card = new X16Emulator.SdCard(4100, new NullLogger());
        using var emulator = new Emulator();
        emulator.LoadSdCard(card);

        const uint block = 0x80_0000 + 100;     // 4GB is block $800000
        Assert.IsTrue(block < card.Size / 512);

        var pattern = MakePattern(11);
        pattern.CopyTo(emulator.Memory[Pattern..]);

        await Run(emulator, Write(block) + Read(block));

        CollectionAssert.AreEqual(pattern, BlockFromReply(ReplyBytes(emulator)));
    }
}
