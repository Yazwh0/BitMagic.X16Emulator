using BitMagic.X16Emulator.Snapshot;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static BitMagic.X16Emulator.Emulator.VeraState;

namespace BitMagic.X16Emulator.Tests.Debugging.Vram;

[TestClass]
public class STZ
{
    [TestMethod]
    public async Task Write_Data0_Abs()
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
                stz DATA0   ; $811
                stz DATA0   ; $814
                stz DATA0   ; $817 - break before the read happens
                stz DATA0
                stz DATA0
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x817)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f23, 0x03)
            .Is(MemoryAreas.Vram, 0x00, new byte[] { 0x00, 0x00, 0x00, 0x03, 0x04, 0x05, 0x06 })
            .ResultIs(Emulator.BreakpointSourceType.Vram)
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
        emulator.X = 0x23;

        // stz $abcd, x which doesn't cross a boundary will cause a read before the write
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp            ; $810
                stz $9f00, x   ; $811
                stz $9f00, x   ; $814
                stz $9f00, x   ; $817 - break before the read happens
                stz $9f00, x
                stz $9f00, x
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x817)
            .Is(MemoryAreas.Ram, 0x9f20, 0x05)
            .Is(MemoryAreas.Ram, 0x9f23, 0x05)
            .Is(MemoryAreas.Vram, 0x00, new byte[] { 0x00, 0x01, 0x00, 0x03, 0x00, 0x05, 0x06, 0x07, 0x08 })
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
        emulator.X = 0x24;

        // stz $abcd, x which doesn't cross a boundary will cause a read before the write
        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp            ; $810
                stz $9eff, x   ; $811
                stz $9eff, x   ; $814
                stz $9eff, x   ; $817 - break before the read happens
                stz $9eff, x
                stz $9eff, x
                stp",
            emulator);

        var snapshot = emulator.Snapshot();

        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.Pc, 0x817)
            .Is(MemoryAreas.Ram, 0x9f20, 0x03)
            .Is(MemoryAreas.Ram, 0x9f23, 0x03)
            .Is(MemoryAreas.Vram, 0x00, new byte[] { 0x00, 0x00, 0x00, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 })
            .ResultIs(Emulator.BreakpointSourceType.Vram)
            .AssertNoOtherChanges();
    }
}