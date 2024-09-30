using BitMagic.X16Emulator.Snapshot;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static BitMagic.X16Emulator.Emulator.VeraState;

namespace BitMagic.X16Emulator.Tests.Debugging.Vram;

[TestClass]
public class STX
{
    [TestMethod]
    public async Task Write_Data0_Abs()
    {
        var emulator = new Emulator();

        emulator.VramBreakpoints[0x0003] = (byte)VeraBreakpointType.Write;
        emulator.Vera.Vram.Set(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 });
        emulator.Vera.Data0_Address = 0x00001;
        emulator.Vera.Data0_Step = 0x01;
        emulator.X = 0xaa;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stp         ; $810
                stx DATA0   ; $811
                stx DATA0   ; $814
                stx DATA0   ; $817 - break before the read happens
                stx DATA0
                stx DATA0
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
}