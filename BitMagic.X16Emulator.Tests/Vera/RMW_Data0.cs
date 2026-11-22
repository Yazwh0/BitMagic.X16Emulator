using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

// Read-modify-write instructions on the 65C02 read the address more than once before they write, and each read of a
// data port steps it. These pin how many reads each instruction does, the value modified is from the first read.
[TestClass]
public class RMW_Data0
{
    private static Emulator Setup()
    {
        var emulator = new Emulator();

        emulator.Vera.Data0_Step = 1;
        emulator.Vera.Data0_Address = 0x0000;
        for (var i = 0; i < 8; i++)
            emulator.Vera.Vram[i] = (byte)(0x10 * (i + 1));

        return emulator;
    }

    // vram holds $10, $20, $30... so after a write at writeAddress the rest are unchanged
    private static void AssertVram(Emulator emulator, int writeAddress, byte value, int finalAddress)
    {
        for (var i = 0; i < 8; i++)
            Assert.AreEqual(i == writeAddress ? value : (byte)(0x10 * (i + 1)), emulator.Vera.Vram[i], $"vram {i}");

        Assert.AreEqual(finalAddress, emulator.Vera.Data0_Address);
        Assert.AreEqual(emulator.Vera.Vram[finalAddress], emulator.Memory[0x9F23]);
    }

    [TestMethod]
    [DataRow("asl DATA0", 0x20)]
    [DataRow("lsr DATA0", 0x08)]
    [DataRow("rol DATA0", 0x20)]
    [DataRow("ror DATA0", 0x08)]
    [DataRow("asl $9f00, x", 0x20)]
    [DataRow("lsr $9f00, x", 0x08)]
    [DataRow("rol $9f00, x", 0x20)]
    [DataRow("ror $9f00, x", 0x08)]
    public async Task Shifts_ReadTwiceThenWrite(string instruction, int value)
    {
        using var emulator = Setup();
        emulator.X = 0x23;

        await X16TestHelper.Emulate($@"
                .machine CommanderX16R40
                .org $810
                {instruction}
                stp",
                emulator);

        AssertVram(emulator, 2, (byte)value, 3);
    }

    // crossing a page the first read is from the wrong page, so only two reads of the port
    [TestMethod]
    [DataRow("inc $9eff, x", 0x11)]
    [DataRow("dec $9eff, x", 0x0f)]
    public async Task IncDec_AbsX_PageCross(string instruction, int value)
    {
        using var emulator = Setup();
        emulator.X = 0x24;

        await X16TestHelper.Emulate($@"
                .machine CommanderX16R40
                .org $810
                {instruction}
                stp",
                emulator);

        AssertVram(emulator, 2, (byte)value, 3);
    }

    [TestMethod]
    [DataRow("sta $9f00, x", 0xaa)]
    [DataRow("sta $9f00, y", 0xaa)]
    [DataRow("stz $9f00, x", 0x00)]
    public async Task Store_Indexed_NoPageCross(string instruction, int value)
    {
        using var emulator = Setup();
        emulator.A = 0xaa;
        emulator.X = 0x23;
        emulator.Y = 0x23;

        await X16TestHelper.Emulate($@"
                .machine CommanderX16R40
                .org $810
                {instruction}
                stp",
                emulator);

        AssertVram(emulator, 1, (byte)value, 2);   // one dummy read of the port, then the write
    }

    [TestMethod]
    [DataRow("sta $9eff, x", 0xaa)]
    [DataRow("sta $9eff, y", 0xaa)]
    [DataRow("stz $9eff, x", 0x00)]
    public async Task Store_Indexed_PageCross(string instruction, int value)
    {
        using var emulator = Setup();
        emulator.A = 0xaa;
        emulator.X = 0x24;
        emulator.Y = 0x24;

        await X16TestHelper.Emulate($@"
                .machine CommanderX16R40
                .org $810
                {instruction}
                stp",
                emulator);

        AssertVram(emulator, 0, (byte)value, 1);
    }
}
