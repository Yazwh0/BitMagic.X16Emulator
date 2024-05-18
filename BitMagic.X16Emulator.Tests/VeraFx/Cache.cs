using Microsoft.VisualStudio.TestTools.UnitTesting;
using BitMagic.X16Emulator.Snapshot;

namespace BitMagic.X16Emulator.Tests.Vera.Fx;

[TestClass]
public class Cache
{
    [TestMethod]
    public async Task FillDirect()
    {
        var emulator = new Emulator();

        emulator.A = 0x01;
        emulator.Vera.DcSel = 0x06;
        emulator.VeraFx.CacheIndex = 0;
        emulator.VeraFx.CacheWrite = false;
        emulator.VeraFx.TwoByteCacheIncr = false;

        var (_, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                stp
                sta FX_CACHE_L
                inc
                sta FX_CACHE_M
                inc
                sta FX_CACHE_H
                inc
                sta FX_CACHE_U
                stp",
                emulator);

        snapshot.Snap();
        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.A, 0x04)
            .AssertNoOtherChanges();

        Assert.AreEqual(0x04030201u, emulator.VeraFx.Cache);
    }

    [TestMethod]
    public async Task FillVram()
    {
        var emulator = new Emulator();

        emulator.A = 0x00;
        emulator.VeraFx.CacheIndex = 0;
        emulator.VeraFx.Cachefill = true;
        emulator.VeraFx.TwoByteCacheIncr = false;

        emulator.Vera.Vram[0x00] = 0x01;
        emulator.Vera.Vram[0x01] = 0x02;
        emulator.Vera.Vram[0x02] = 0x03;
        emulator.Vera.Vram[0x03] = 0x04;
        emulator.Vera.Data0_Address = 0x00000;
        emulator.Vera.Data0_Step = 0x01;

        var (_, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                stp
                lda DATA0
                lda DATA0
                lda DATA0
                lda DATA0
                stp",
                emulator);

        snapshot.Snap();
        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .Is(Registers.A, 0x04)
            .Is(MemoryAreas.Ram, 0x9f20, 0x04)
            .Is(MemoryAreas.Ram, 0x9f23, 0x00) // next read is 0
            .AssertNoOtherChanges();

        Assert.AreEqual(0x04030201u, emulator.VeraFx.Cache);
    }
}