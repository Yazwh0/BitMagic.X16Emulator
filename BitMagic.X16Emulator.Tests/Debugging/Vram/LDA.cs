using BitMagic.X16Emulator.Snapshot;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static BitMagic.X16Emulator.Emulator.VeraState;

namespace BitMagic.X16Emulator.Tests.Debugging.Vram;

[TestClass]
public class LDA
{
    [TestMethod]
    public async Task Read_Data0_Abs()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp         ; $810
                lda DATA0   ; $811
                lda DATA0   ; $814
                lda DATA0   ; $817 - break before the read happens
                lda DATA0
                lda DATA0
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x817)
            .Is(Registers.A, 0x02)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f23, 0x03)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Read_Data0_NoBreak()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Write;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp         ; $810
                lda DATA0   ; $811
                lda DATA0   ; $814
                lda DATA0   ; $817 - breakpoint here, shouldn't fire
                lda DATA0   ; $81a
                lda DATA0   ; $81d
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x821)
            .Is(Registers.A, 0x05)
            .Is(MemoryAreas.Ram, 0x9f20, 0x06)
            .Is(MemoryAreas.Ram, 0x9f23, 0x06)
            .ResultIs(Emulator.EmulatorResult.DebugOpCode)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Read_Data0_Continue()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp         ; $810
                lda DATA0   ; $811
                lda DATA0   ; $814
                lda DATA0   ; $817 - break before the read happens
                stp         ; $81a
                lda DATA0
                lda DATA0
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Snap();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x81b)    // PC is next instruction
            .Is(Registers.A, 0x03)
            .Is(MemoryAreas.Ram, 0x9f20, 0x04)
            .Is(MemoryAreas.Ram, 0x9f23, 0x04)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Read_Data0_X()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;
        emulator.X = 0x23;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp            ; $810
                lda $9f00, x   ; $811
                lda $9f00, x   ; $814
                lda $9f00, x   ; $817 - break before the read happens
                lda $9f00, x
                lda $9f00, x
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x817)
            .Is(Registers.A, 0x02)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f23, 0x03)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Read_Data0_Y()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;
        emulator.Y = 0x23;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp            ; $810
                lda $9f00, y   ; $811
                lda $9f00, y   ; $814
                lda $9f00, y   ; $817 - break before the read happens
                lda $9f00, y
                lda $9f00, y
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x817)
            .Is(Registers.A, 0x02)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f23, 0x03)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Read_Data0_Ind()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Memory.Set(new byte[] { 0x23, 0x9f }, 0x10);
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp         ; $810
                lda ($10)   ; $811
                lda ($10)   ; $813
                lda ($10)   ; $815 - break before the read happens
                lda ($10)
                lda ($10)
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x815)
            .Is(Registers.A, 0x02)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f23, 0x03)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Read_Data0_XInd()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Memory.Set(new byte[] { 0x23, 0x9f }, 0x10);
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;
        emulator.X = 0x06;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp            ; $810
                lda ($0a, x)   ; $811
                lda ($0a, x)   ; $813
                lda ($0a, x)   ; $815 - break before the read happens
                lda ($0a, x)
                lda ($0a, x)
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x815)
            .Is(Registers.A, 0x02)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f23, 0x03)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Read_Data0_YInd()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Memory.Set(new byte[] { 0x20, 0x9f }, 0x10);
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;
        emulator.Y = 0x03;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp            ; $810
                lda ($10), y   ; $811
                lda ($10), y   ; $813
                lda ($10), y   ; $815 - break before the read happens
                lda ($10), y
                lda ($10), y
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x815)
            .Is(Registers.A, 0x02)
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
        emulator.Vera.AddrSel = true;
        emulator.Vera.Data1_Address = 0x00001;
        emulator.Vera.Data1_Step = 0x01;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp         ; $810
                lda DATA1   ; $811
                lda DATA1   ; $814
                lda DATA1   ; $817 - break before the read happens
                lda DATA1
                lda DATA1
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x817)
            .Is(Registers.A, 0x02)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f24, 0x03)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Read_Data1_NoBreak()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Write;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Vera.Data1_Address = 0x00001;
        emulator.Vera.AddrSel = true;
        emulator.Vera.Data1_Step = 0x01;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp         ; $810
                lda DATA1   ; $811
                lda DATA1   ; $814
                lda DATA1   ; $817 - breakpoint here, shouldn't fire
                lda DATA1   ; $81a
                lda DATA1   ; $81d
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x821)
            .Is(Registers.A, 0x05)
            .Is(MemoryAreas.Ram, 0x9f20, 0x06)
            .Is(MemoryAreas.Ram, 0x9f24, 0x06)
            .ResultIs(Emulator.EmulatorResult.DebugOpCode)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Read_Data1_Continue()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Vera.AddrSel = true;
        emulator.Vera.Data1_Address = 0x00001;
        emulator.Vera.Data1_Step = 0x01;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp         ; $810
                lda DATA1   ; $811
                lda DATA1   ; $814
                lda DATA1   ; $817 - break before the read happens
                stp         ; $81a
                lda DATA1
                lda DATA1
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Snap();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x81b)    // PC is next instruction
            .Is(Registers.A, 0x03)
            .Is(MemoryAreas.Ram, 0x9f20, 0x04)
            .Is(MemoryAreas.Ram, 0x9f24, 0x04)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Read_Data1_Lda_X()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Vera.AddrSel = true;
        emulator.Vera.Data1_Address = 0x00001;
        emulator.Vera.Data1_Step = 0x01;
        emulator.X = 0x24;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp            ; $810
                lda $9f00, x   ; $811
                lda $9f00, x   ; $814
                lda $9f00, x   ; $817 - break before the read happens
                lda $9f00, x
                lda $9f00, x
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x817)
            .Is(Registers.A, 0x02)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f24, 0x03)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Read_Data1_Lda_Y()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Vera.AddrSel = true;
        emulator.Vera.Data1_Address = 0x00001;
        emulator.Vera.Data1_Step = 0x01;
        emulator.Y = 0x24;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp            ; $810
                lda $9f00, y   ; $811
                lda $9f00, y   ; $814
                lda $9f00, y   ; $817 - break before the read happens
                lda $9f00, y
                lda $9f00, y
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x817)
            .Is(Registers.A, 0x02)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f24, 0x03)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Read_Data1_Ind()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Vera.AddrSel = true;
        emulator.Memory.Set(new byte[] { 0x24, 0x9f }, 0x10);
        emulator.Vera.Data1_Address = 0x00001;
        emulator.Vera.Data1_Step = 0x01;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp         ; $810
                lda ($10)   ; $811
                lda ($10)   ; $813
                lda ($10)   ; $815 - break before the read happens
                lda ($10)
                lda ($10)
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x815)
            .Is(Registers.A, 0x02)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f24, 0x03)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Read_Data1_XInd()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Memory.Set(new byte[] { 0x24, 0x9f }, 0x10);
        emulator.Vera.AddrSel = true;
        emulator.Vera.Data1_Address = 0x00001;
        emulator.Vera.Data1_Step = 0x01;
        emulator.X = 0x06;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp            ; $810
                lda ($0a, x)   ; $811
                lda ($0a, x)   ; $813
                lda ($0a, x)   ; $815 - break before the read happens
                lda ($0a, x)
                lda ($0a, x)
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x815)
            .Is(Registers.A, 0x02)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f24, 0x03)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Read_Data1_YInd()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Memory.Set(new byte[] { 0x20, 0x9f }, 0x10);
        emulator.Vera.AddrSel = true;
        emulator.Vera.Data1_Address = 0x00001;
        emulator.Vera.Data1_Step = 0x01;
        emulator.Y = 0x04;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp            ; $810
                lda ($10), y   ; $811
                lda ($10), y   ; $813
                lda ($10), y   ; $815 - break before the read happens
                lda ($10), y
                lda ($10), y
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x815)
            .Is(Registers.A, 0x02)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f24, 0x03)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

}