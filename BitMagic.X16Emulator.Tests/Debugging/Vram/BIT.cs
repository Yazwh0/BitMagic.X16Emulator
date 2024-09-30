using BitMagic.X16Emulator.Snapshot;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static BitMagic.X16Emulator.Emulator.VeraState;

namespace BitMagic.X16Emulator.Tests.Debugging.Vram;

[TestClass]
public class BIT
{
    [TestMethod]
    public async Task Read_Data0_Abs()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;
        emulator.A = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp         ; $810
                bit DATA0   ; $811
                bit DATA0   ; $814
                bit DATA0   ; $817 - break before the read happens
                bit DATA0
                bit DATA0
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x817)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f23, 0x03)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Read_Data0_AbsX()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;
        emulator.X = 0x23;
        emulator.A = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp            ; $810
                bit $9f00, x   ; $811
                bit $9f00, x   ; $814
                bit $9f00, x   ; $817 - break before the read happens
                bit $9f00, x
                bit $9f00, x
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x817)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f23, 0x03)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Read_Data1_Abs()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Vera.Data1_Address = 0x00001;
        emulator.Vera.Data1_Step = 0x01;
        emulator.Vera.AddrSel = true;
        emulator.A = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp         ; $810
                bit DATA1   ; $811
                bit DATA1   ; $814
                bit DATA1   ; $817 - break before the read happens
                bit DATA1
                bit DATA1
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x817)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f24, 0x03)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Read_Data1_AbsX()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Vera.Data1_Address = 0x00001;
        emulator.Vera.Data1_Step = 0x01;
        emulator.Vera.AddrSel = true;
        emulator.X = 0x24;
        emulator.A = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp            ; $810
                bit $9f00, x   ; $811
                bit $9f00, x   ; $814
                bit $9f00, x   ; $817 - break before the read happens
                bit $9f00, x
                bit $9f00, x
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x817)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f24, 0x03)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }
}