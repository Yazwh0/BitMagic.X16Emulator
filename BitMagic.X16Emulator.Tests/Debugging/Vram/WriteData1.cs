using BitMagic.X16Emulator.Snapshot;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static BitMagic.X16Emulator.Emulator.VeraState;

namespace BitMagic.X16Emulator.Tests.Debugging.Vram;

// Write breakpoints through DATA1, which were never checked. The store instructions all write the same way, so each
// test runs for every one of them.
[TestClass]
public class WriteData1
{
    private static async Task<Emulator> Run(string instruction, VeraBreakpointType breakpoint)
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)breakpoint;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Vera.AddrSel = true;
        emulator.Vera.Data1_Address = 0x00001;
        emulator.Vera.Data1_Step = 0x01;
        emulator.A = 0xaa;
        emulator.X = 0xaa;
        emulator.Y = 0xaa;

        await X16TestHelper.Emulate(@$"
                .machine CommanderX16R40
                .org $810
                stp                     ; $810
                {instruction} DATA1     ; $811
                {instruction} DATA1     ; $814
                {instruction} DATA1     ; $817 - break before the write happens
                {instruction} DATA1
                {instruction} DATA1
                stp",
            emulator);

        return emulator;
    }

    private static byte Value(string instruction) => instruction == "stz" ? (byte)0x00 : (byte)0xaa;

    [TestMethod]
    [DataRow("sta")]
    [DataRow("stx")]
    [DataRow("sty")]
    [DataRow("stz")]
    public async Task Write_Data1_Abs(string instruction)
    {
        using var emulator = await Run(instruction, VeraBreakpointType.Write);
        var v = Value(instruction);

        var snapshot = emulator.Snapshot();

        emulator.EmulateWithTimeout();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x817)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f24, 0x03)
            .Is(MemoryAreas.Vram, 0x00, new byte[] { 0x00, v, v, 0x03, 0x04, 0x05, 0x06 })
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    [DataRow("sta")]
    [DataRow("stx")]
    [DataRow("sty")]
    [DataRow("stz")]
    public async Task Write_Data1_Abs_NoBreak(string instruction)
    {
        using var emulator = await Run(instruction, VeraBreakpointType.Read);
        var v = Value(instruction);

        var snapshot = emulator.Snapshot();

        emulator.EmulateWithTimeout();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x821)
            .Is(MemoryAreas.Ram, 0x9f20, 0x06)
            .Is(MemoryAreas.Ram, 0x9f24, 0x06)
            .Is(MemoryAreas.Vram, 0x00, new byte[] { 0x00, v, v, v, v, v, 0x06 })
            .AssertNoOtherChanges();
    }

    [TestMethod]
    [DataRow("sta")]
    [DataRow("stx")]
    [DataRow("sty")]
    [DataRow("stz")]
    public async Task Write_Data1_Abs_Continue(string instruction)
    {
        using var emulator = await Run(instruction, VeraBreakpointType.Write);
        var v = Value(instruction);

        var snapshot = emulator.Snapshot();

        emulator.EmulateWithTimeout();

        snapshot.Snap();

        emulator.EmulateWithTimeout();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x821)
            .Is(MemoryAreas.Ram, 0x9f20, 0x06)
            .Is(MemoryAreas.Ram, 0x9f24, 0x06)
            .Is(MemoryAreas.Vram, 0x00, new byte[] { 0x00, v, v, v, v, v, 0x06 })
            .AssertNoOtherChanges();
    }

    // A read-modify-write (eg inc) steps the port between the reads and the write, so the write lands further on and
    // that's where it breaks. inc abs reads twice, writes two steps on and moves three, so the second inc starts at $04
    // and writes $06. Both ports work the same way.
    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public async Task ReadModifyWrite_BreaksOnTheWrittenAddress(int port)
    {
        const int written = 0x06;
        const int secondStart = 0x04;

        using var emulator = new Emulator();

        emulator.VramBreakpoints[written] = (byte)VeraBreakpointType.Write;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80 });
        emulator.Vera.AddrSel = port == 1;
        if (port == 0)
        {
            emulator.Vera.Data0_Address = 0x00001;
            emulator.Vera.Data0_Step = 0x01;
        }
        else
        {
            emulator.Vera.Data1_Address = 0x00001;
            emulator.Vera.Data1_Step = 0x01;
        }

        await X16TestHelper.Emulate(@$"
                .machine CommanderX16R40
                .org $810
                stp                 ; $810
                inc DATA{port}      ; $811
                inc DATA{port}      ; $814 - breaks here
                inc DATA{port}      ; $817
                stp",
            emulator);

        var before = emulator.Vera.Vram[written];

        emulator.EmulateWithTimeout();

        Assert.AreEqual(Emulator.EmulatorResult.Breakpoint, emulator.Result);
        Assert.AreEqual(0x814, emulator.Pc);
        Assert.AreEqual(before, emulator.Vera.Vram[written], "The write hasn't happened.");
        Assert.AreEqual(secondStart, port == 0 ? emulator.Vera.Data0_Address : emulator.Vera.Data1_Address, "The address is where the second inc starts.");

        emulator.EmulateWithTimeout();

        Assert.AreEqual(0x81b, emulator.Pc, "Carries on to the end.");
    }
}
