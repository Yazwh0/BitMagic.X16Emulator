using BitMagic.X16Emulator.Snapshot;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static BitMagic.X16Emulator.Emulator.VeraState;

namespace BitMagic.X16Emulator.Tests.Debugging.Vram;

[TestClass]
public class STA
{
    [TestMethod]
    public async Task Write_Data0_Abs()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Write;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;
        emulator.A = 0xaa;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp         ; $810
                sta DATA0   ; $811
                sta DATA0   ; $814
                sta DATA0   ; $817 - break before the read happens
                sta DATA0
                sta DATA0
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x817)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f23, 0x03)
            .Is(MemoryAreas.Vram, 0x00, new byte[] { 0x00, 0xaa, 0xaa, 0x03, 0x04, 0x05, 0x06 })
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Write_Data0_Abs_NoBreak()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;
        emulator.A = 0xaa;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp         ; $810
                sta DATA0   ; $811
                sta DATA0   ; $814
                sta DATA0   ; $817 - break before the read happens
                sta DATA0
                sta DATA0
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x821)
            .Is(MemoryAreas.Ram, 0x9f20, 0x06)
            .Is(MemoryAreas.Ram, 0x9f23, 0x06)
            .Is(MemoryAreas.Vram, 0x00, new byte[] { 0x00, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, 0x06 })
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Write_Data0_Abs_Continue()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Write;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;
        emulator.A = 0xaa;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp         ; $810
                sta DATA0   ; $811
                sta DATA0   ; $814
                sta DATA0   ; $817 - break before the read happens
                sta DATA0
                sta DATA0
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Snap();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x821)
            .Is(MemoryAreas.Ram, 0x9f20, 0x06)
            .Is(MemoryAreas.Ram, 0x9f23, 0x06)
            .Is(MemoryAreas.Vram, 0x00, new byte[] { 0x00, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, 0x06 })
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Write_Data0_X_NoCross()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0006] = (byte)VeraBreakpointType.Write;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 });
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;
        emulator.A = 0xaa;
        emulator.X = 0x23;

        // sta $abcd, x which doesn't cross a boundary will cause a read before the write
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp            ; $810
                sta $9f00, x   ; $811
                sta $9f00, x   ; $814
                sta $9f00, x   ; $817 - break before the read happens
                sta $9f00, x
                sta $9f00, x
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x817)
            .Is(MemoryAreas.Ram, 0x9f20, 0x05)
            .Is(MemoryAreas.Ram, 0x9f23, 0x05)
            .Is(MemoryAreas.Vram, 0x00, new byte[] { 0x00, 0x01, 0xaa, 0x03, 0xaa, 0x05, 0x06, 0x07, 0x08 })
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Write_Data0_X_Cross()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Write;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 });
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;
        emulator.A = 0xaa;
        emulator.X = 0x24;

        // sta $abcd, x which doesn't cross a boundary will cause a read before the write
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp            ; $810
                sta $9eff, x   ; $811
                sta $9eff, x   ; $814
                sta $9eff, x   ; $817 - break before the read happens
                sta $9eff, x
                sta $9eff, x
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x817)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f23, 0x03)
            .Is(MemoryAreas.Vram, 0x00, new byte[] { 0x00, 0xaa, 0xaa, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 })
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Write_Data0_Y_NoCross()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0006] = (byte)VeraBreakpointType.Write;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 });
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;
        emulator.A = 0xaa;
        emulator.Y = 0x23;

        // sta $abcd, y which doesn't cross a boundary will cause a read before the write
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp            ; $810
                sta $9f00, y   ; $811
                sta $9f00, y   ; $814
                sta $9f00, y   ; $817 - break before the read happens
                sta $9f00, y
                sta $9f00, y
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x817)
            .Is(MemoryAreas.Ram, 0x9f20, 0x05)
            .Is(MemoryAreas.Ram, 0x9f23, 0x05)
            .Is(MemoryAreas.Vram, 0x00, new byte[] { 0x00, 0x01, 0xaa, 0x03, 0xaa, 0x05, 0x06, 0x07, 0x08 })
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Write_Data0_Y_Cross()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Write;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 });
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;
        emulator.A = 0xaa;
        emulator.Y = 0x24;

        // sta $abcd, y which doesn't cross a boundary will cause a read before the write
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp            ; $810
                sta $9eff, y   ; $811
                sta $9eff, y   ; $814
                sta $9eff, y   ; $817 - break before the read happens
                sta $9eff, y
                sta $9eff, y
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x817)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f23, 0x03)
            .Is(MemoryAreas.Vram, 0x00, new byte[] { 0x00, 0xaa, 0xaa, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 })
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Write_Data0_Ind()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Write;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Memory.Set(new byte[] { 0x23, 0x9f }, 0x10);
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;
        emulator.A = 0xaa;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp         ; $810
                sta ($10)   ; $811
                sta ($10)   ; $813
                sta ($10)   ; $815 - break before the read happens
                sta ($10)
                sta ($10)
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x815)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f23, 0x03)
            .Is(MemoryAreas.Vram, 0x00, new byte[] { 0x00, 0xaa, 0xaa, 0x03, 0x04, 0x05, 0x06 })
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Write_Data0_IndX()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Write;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Memory.Set(new byte[] { 0x23, 0x9f }, 0x10);
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;
        emulator.A = 0xaa;
        emulator.X = 0x06;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp           ; $810
                sta ($0a,x)   ; $811
                sta ($0a,x)   ; $813
                sta ($0a,x)   ; $815 - break before the read happens
                sta ($0a,x)
                sta ($0a,x)
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x815)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f23, 0x03)
            .Is(MemoryAreas.Vram, 0x00, new byte[] { 0x00, 0xaa, 0xaa, 0x03, 0x04, 0x05, 0x06 })
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Write_Data0_IndY()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Write;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Memory.Set(new byte[] { 0x20, 0x9f }, 0x10);
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;
        emulator.A = 0xaa;
        emulator.Y = 0x03;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp            ; $810
                sta ($10), y   ; $811
                sta ($10), y   ; $813
                sta ($10), y   ; $815 - break before the read happens
                sta ($10), y
                sta ($10), y
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x815)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f23, 0x03)
            .Is(MemoryAreas.Vram, 0x00, new byte[] { 0x00, 0xaa, 0xaa, 0x03, 0x04, 0x05, 0x06 })
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }
}