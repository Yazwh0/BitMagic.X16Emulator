using BitMagic.X16Emulator.Snapshot;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static BitMagic.X16Emulator.Emulator.VeraState;

namespace BitMagic.X16Emulator.Tests.Debugging.Vram;

[TestClass]
public class SBC
{
    [TestMethod]
    public async Task Read_Data0_Abs()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x10, 0x11, 0x13, 0x17, 0x14, 0x1f, 0x3f });
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;
        emulator.Carry = true;
        emulator.A = 0x80;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp         ; $810
                sbc DATA0   ; $811
                sbc DATA0   ; $814
                sbc DATA0   ; $817 - break before the read happens
                sbc DATA0
                sbc DATA0
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x817)
            .Is(Registers.A, 0x5c)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f23, 0x17)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    public async Task Read_Data0_Lda_X()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x10, 0x11, 0x13, 0x17, 0x14, 0x1f, 0x3f });
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;
        emulator.Carry = true;
        emulator.X = 0x23;
        emulator.A = 0x80;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp            ; $810
                sbc $9f00, x   ; $811
                sbc $9f00, x   ; $814
                sbc $9f00, x   ; $817 - break before the read happens
                sbc $9f00, x
                sbc $9f00, x
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x817)
            .Is(Registers.A, 0x5c)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f23, 0x17)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Read_Data0_Lda_Y()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x10, 0x11, 0x13, 0x17, 0x14, 0x1f, 0x3f });
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;
        emulator.Carry = true;
        emulator.Y = 0x23;
        emulator.A = 0x80;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp            ; $810
                sbc $9f00, y   ; $811
                sbc $9f00, y   ; $814
                sbc $9f00, y   ; $817 - break before the read happens
                sbc $9f00, y
                sbc $9f00, y
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x817)
            .Is(Registers.A, 0x5c)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f23, 0x17)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Read_Data0_Ind()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x10, 0x11, 0x13, 0x17, 0x14, 0x1f, 0x3f });
        emulator.Memory.Set(new byte[] { 0x23, 0x9f }, 0x10);
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;
        emulator.Carry = true;
        emulator.A = 0x80;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp         ; $810
                sbc ($10)   ; $811
                sbc ($10)   ; $813
                sbc ($10)   ; $815 - break before the read happens
                sbc ($10)
                sbc ($10)
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x815)
            .Is(Registers.A, 0x5c)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f23, 0x17)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Read_Data0_XInd()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x10, 0x11, 0x13, 0x17, 0x14, 0x1f, 0x3f });
        emulator.Memory.Set(new byte[] { 0x23, 0x9f }, 0x10);
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;
        emulator.Carry = true;
        emulator.X = 0x06;
        emulator.A = 0x80;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp            ; $810
                sbc ($0a, x)   ; $811
                sbc ($0a, x)   ; $813
                sbc ($0a, x)   ; $815 - break before the read happens
                sbc ($0a, x)
                sbc ($0a, x)
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x815)
            .Is(Registers.A, 0x5c)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f23, 0x17)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Read_Data0_YInd()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x10, 0x11, 0x13, 0x17, 0x14, 0x1f, 0x3f });
        emulator.Memory.Set(new byte[] { 0x20, 0x9f }, 0x10);
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;
        emulator.Carry = true;
        emulator.Y = 0x03;
        emulator.A = 0x80;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp            ; $810
                sbc ($10), y   ; $811
                sbc ($10), y   ; $813
                sbc ($10), y   ; $815 - break before the read happens
                sbc ($10), y
                sbc ($10), y
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x815)
            .Is(Registers.A, 0x5c)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f23, 0x17)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Read_Data1_Abs()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x10, 0x11, 0x13, 0x17, 0x14, 0x1f, 0x3f });
        emulator.Vera.AddrSel = true;
        emulator.Vera.Data1_Address = 0x00001;
        emulator.Vera.Data1_Step = 0x01;
        emulator.Carry = true;
        emulator.A = 0x80;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp         ; $810
                sbc DATA1   ; $811
                sbc DATA1   ; $814
                sbc DATA1   ; $817 - break before the read happens
                sbc DATA1
                sbc DATA1
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x817)
            .Is(Registers.A, 0x5c)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f24, 0x17)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    public async Task Read_Data1_Lda_X()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x10, 0x11, 0x13, 0x17, 0x14, 0x1f, 0x3f });
        emulator.Vera.AddrSel = true;
        emulator.Vera.Data1_Address = 0x00001;
        emulator.Vera.Data1_Step = 0x01;
        emulator.Carry = true;
        emulator.X = 0x24;
        emulator.A = 0x80;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp            ; $810
                sbc $9f00, x   ; $811
                sbc $9f00, x   ; $814
                sbc $9f00, x   ; $817 - break before the read happens
                sbc $9f00, x
                sbc $9f00, x
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x817)
            .Is(Registers.A, 0x5c)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f24, 0x17)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Read_Data1_Lda_Y()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x10, 0x11, 0x13, 0x17, 0x14, 0x1f, 0x3f });
        emulator.Vera.AddrSel = true;
        emulator.Vera.Data1_Address = 0x00001;
        emulator.Vera.Data1_Step = 0x01;
        emulator.Carry = true;
        emulator.Y = 0x24;
        emulator.A = 0x80;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp            ; $810
                sbc $9f00, y   ; $811
                sbc $9f00, y   ; $814
                sbc $9f00, y   ; $817 - break before the read happens
                sbc $9f00, y
                sbc $9f00, y
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x817)
            .Is(Registers.A, 0x5c)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f24, 0x17)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Read_Data1_Ind()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x10, 0x11, 0x13, 0x17, 0x14, 0x1f, 0x3f });
        emulator.Memory.Set(new byte[] { 0x24, 0x9f }, 0x10);
        emulator.Vera.AddrSel = true;
        emulator.Vera.Data1_Address = 0x00001;
        emulator.Vera.Data1_Step = 0x01;
        emulator.Carry = true;
        emulator.A = 0x80;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp         ; $810
                sbc ($10)   ; $811
                sbc ($10)   ; $813
                sbc ($10)   ; $815 - break before the read happens
                sbc ($10)
                sbc ($10)
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x815)
            .Is(Registers.A, 0x5c)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f24, 0x17)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Read_Data1_XInd()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x10, 0x11, 0x13, 0x17, 0x14, 0x1f, 0x3f });
        emulator.Memory.Set(new byte[] { 0x24, 0x9f }, 0x10);
        emulator.Vera.AddrSel = true;
        emulator.Vera.Data1_Address = 0x00001;
        emulator.Vera.Data1_Step = 0x01;
        emulator.Carry = true;
        emulator.X = 0x06;
        emulator.A = 0x80;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp            ; $810
                sbc ($0a, x)   ; $811
                sbc ($0a, x)   ; $813
                sbc ($0a, x)   ; $815 - break before the read happens
                sbc ($0a, x)
                sbc ($0a, x)
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x815)
            .Is(Registers.A, 0x5c)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f24, 0x17)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task Read_Data1_YInd()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Read;
        emulator.Vera.Vram.Set(new byte[] { 0x10, 0x11, 0x13, 0x17, 0x14, 0x1f, 0x3f });
        emulator.Memory.Set(new byte[] { 0x20, 0x9f }, 0x10);
        emulator.Vera.AddrSel = true;
        emulator.Vera.Data1_Address = 0x00001;
        emulator.Vera.Data1_Step = 0x01;
        emulator.Carry = true;
        emulator.Y = 0x04;
        emulator.A = 0x80;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp            ; $810
                sbc ($10), y   ; $811
                sbc ($10), y   ; $813
                sbc ($10), y   ; $815 - break before the read happens
                sbc ($10), y
                sbc ($10), y
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x815)
            .Is(Registers.A, 0x5c)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f24, 0x17)
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }

}