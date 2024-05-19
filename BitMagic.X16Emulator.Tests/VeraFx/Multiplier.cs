using Microsoft.VisualStudio.TestTools.UnitTesting;
using BitMagic.X16Emulator.Snapshot;

namespace BitMagic.X16Emulator.Tests.Vera.Fx;

[TestClass]
public class Multiplier
{
    [TestMethod]
    public async Task SetMultiplierFlag()
    {
        var emulator = new Emulator();

        emulator.A = 0x10;
        emulator.Vera.DcSel = 2;

        var (_, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                stp
                sta FX_MULT
                stp",
                emulator);

        snapshot.Snap();
        emulator.Emulate();

        snapshot.Compare().IgnoreVia().AssertNoOtherChanges();

        Assert.IsTrue(emulator.VeraFx.MultiplierEnable);
    }

    [TestMethod]
    public async Task ClearMultiplierFlag()
    {
        var emulator = new Emulator();

        emulator.Vera.DcSel = 2;
        emulator.VeraFx.MultiplierEnable = true;

        var (_, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                stp
                stz FX_MULT
                stp",
                emulator);

        snapshot.Snap();
        emulator.Emulate();

        snapshot.Compare().IgnoreVia().AssertNoOtherChanges();

        Assert.IsFalse(emulator.VeraFx.MultiplierEnable);
    }

    [TestMethod]
    public async Task ClearAccumulator_FxMult()
    {
        var emulator = new Emulator();

        emulator.Vera.DcSel = 2;
        emulator.VeraFx.Accumulator = 1;
        emulator.A = 0b10000000;

        var (_, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                stp
                sta FX_MULT
                stp",
                emulator);

        snapshot.Snap();
        emulator.Emulate();

        snapshot.Compare().IgnoreVia().AssertNoOtherChanges();

        Assert.AreEqual(0u, emulator.VeraFx.Accumulator);
    }

    [TestMethod]
    public async Task ClearAccumulator_FxAccumReset()
    {
        var emulator = new Emulator();

        emulator.Vera.DcSel = 6;
        emulator.VeraFx.Accumulator = 1;

        var (_, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                stp
                lda FX_ACCUM_RESET
                stp",
                emulator);

        snapshot.Snap();
        emulator.Emulate();

        // A contains the 'V' from the Vera version
        snapshot.Compare().IgnoreVia().Is(Registers.A, 0x56).AssertNoOtherChanges();

        Assert.AreEqual(0u, emulator.VeraFx.Accumulator);
    }

    [TestMethod]
    public async Task SetAccumulation_Accumulate()
    {
        var emulator = new Emulator();

        emulator.Vera.DcSel = 2;
        emulator.VeraFx.Accumulator = 0;
        emulator.A = 0b01000000;
        emulator.VeraFx.Cache = 0x01010202;

        var (_, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                stp
                sta FX_MULT
                stp",
                emulator);

        snapshot.Snap();
        emulator.Emulate();

        snapshot.Compare().IgnoreVia().CanChange(Registers.A).CanChange(CpuFlags.Negative).AssertNoOtherChanges();

        Assert.AreEqual(0x101u * 0x202u, emulator.VeraFx.Accumulator);
    }

    [TestMethod]
    public async Task SetAccumulation_AccumulateAdd()
    {
        var emulator = new Emulator();

        emulator.Vera.DcSel = 2;
        emulator.VeraFx.Accumulator = 0x123;
        emulator.A = 0b01000000;
        emulator.VeraFx.Cache = 0x01010202;

        var (_, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                stp
                sta FX_MULT
                stp",
                emulator);

        snapshot.Snap();
        emulator.Emulate();

        snapshot.Compare().IgnoreVia().CanChange(Registers.A).CanChange(CpuFlags.Negative).AssertNoOtherChanges();

        Assert.AreEqual(0x101u * 0x202u + 0x123, emulator.VeraFx.Accumulator);
    }


    [TestMethod]
    public async Task SetAccumulation_AccumulateSub()
    {
        var emulator = new Emulator();

        emulator.Vera.DcSel = 2;
        emulator.VeraFx.Accumulator = 0x123;
        emulator.A = 0b01100000;
        emulator.VeraFx.Cache = 0x01010202;
        emulator.VeraFx.AccumulateDirection = 0;

        var (_, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                stp
                sta FX_MULT
                stp",
                emulator);

        snapshot.Snap();
        emulator.Emulate();

        snapshot.Compare().IgnoreVia().CanChange(Registers.A).CanChange(CpuFlags.Negative).AssertNoOtherChanges();

        Assert.AreEqual(unchecked(0x123u - (0x101u * 0x202u)), emulator.VeraFx.Accumulator);
    }

    [TestMethod]
    public async Task ClearAccumulation_Accumulate()
    {
        var emulator = new Emulator();

        emulator.Vera.DcSel = 6;
        emulator.VeraFx.Accumulator = 0x123;

        var (_, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                stp
                lda FX_ACCUM_RESET
                stp",
                emulator);

        snapshot.Snap();
        emulator.Emulate();

        snapshot.Compare().IgnoreVia().CanChange(Registers.A).AssertNoOtherChanges();

        Assert.AreEqual(0u, emulator.VeraFx.Accumulator);
    }

    [TestMethod]
    public async Task SetAccumulation_FxAccum()
    {
        var emulator = new Emulator();

        emulator.Vera.DcSel = 6;
        emulator.VeraFx.Accumulator = 0;
        emulator.VeraFx.Cache = 0x01010202;

        var (_, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                stp
                lda FX_ACCUM
                stp",
                emulator);

        snapshot.Snap();
        emulator.Emulate();

        snapshot.Compare().IgnoreVia().CanChange(Registers.A).CanChange(CpuFlags.Negative).AssertNoOtherChanges();

        Assert.AreEqual(0x101u * 0x202u, emulator.VeraFx.Accumulator);
    }

    [TestMethod]
    public async Task SetAccumulation_FxAccum_Add()
    {
        var emulator = new Emulator();

        emulator.Vera.DcSel = 6;
        emulator.VeraFx.Accumulator = 0x123;
        emulator.VeraFx.Cache = 0x01010202;

        var (_, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                stp
                lda FX_ACCUM
                stp",
                emulator);

        snapshot.Snap();
        emulator.Emulate();

        snapshot.Compare().IgnoreVia().CanChange(Registers.A).CanChange(CpuFlags.Negative).AssertNoOtherChanges();

        Assert.AreEqual(0x101u * 0x202u + 0x123u, emulator.VeraFx.Accumulator);
    }

    [TestMethod]
    public async Task SetAccumulation_FxAccum_Neg_Add()
    {
        var emulator = new Emulator();

        emulator.Vera.DcSel = 6;
        emulator.VeraFx.Accumulator = 0xfffffffe;
        emulator.VeraFx.Cache = 0xffff0002;

        var (_, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                stp
                lda FX_ACCUM
                stp",
                emulator);

        snapshot.Snap();
        emulator.Emulate();

        snapshot.Compare().IgnoreVia().CanChange(Registers.A).CanChange(CpuFlags.Negative).AssertNoOtherChanges();

        Assert.AreEqual(0xfffffffcu, emulator.VeraFx.Accumulator);
    }

    [TestMethod]
    public async Task SetAccumulation_FxAccum_Sub()
    {
        var emulator = new Emulator();

        emulator.Vera.DcSel = 6;
        emulator.VeraFx.Accumulator = 0x40000;
        emulator.VeraFx.Cache = 0x01010202;
        emulator.VeraFx.AccumulateDirection = 1;

        var (_, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                stp
                lda FX_ACCUM
                stp",
                emulator);

        snapshot.Snap();
        emulator.Emulate();

        snapshot.Compare().IgnoreVia().CanChange(Registers.A).CanChange(CpuFlags.Negative).AssertNoOtherChanges();

        Assert.AreEqual(0x40000u - 0x101u * 0x202u, emulator.VeraFx.Accumulator);
    }

    [TestMethod]
    public async Task SetAccumulatDirection()
    {
        var emulator = new Emulator();

        emulator.Vera.DcSel = 2;
        emulator.VeraFx.AccumulateDirection = 0;
        emulator.A = 0b00100000;

        var (_, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                stp
                sta FX_MULT
                stp",
                emulator);

        snapshot.Snap();
        emulator.Emulate();

        snapshot.Compare().IgnoreVia().AssertNoOtherChanges();

        Assert.AreEqual(1u, emulator.VeraFx.AccumulateDirection);
    }

    [TestMethod]
    public async Task ClearAccumulatDirection()
    {
        var emulator = new Emulator();

        emulator.Vera.DcSel = 2;
        emulator.VeraFx.AccumulateDirection = 1;

        var (_, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                stp
                stz FX_MULT
                stp",
                emulator);

        snapshot.Snap();
        emulator.Emulate();

        snapshot.Compare().IgnoreVia().AssertNoOtherChanges();

        Assert.AreEqual(0u, emulator.VeraFx.AccumulateDirection);
    }

    [TestMethod]
    public async Task MultiplyWithAccumulatorAdd()
    {
        var emulator = new Emulator();

        emulator.VeraFx.AccumulateDirection = 0;
        emulator.VeraFx.Accumulator = 0x123;
        emulator.VeraFx.MultiplierEnable = true;
        emulator.VeraFx.Cache = 0x01010202; // 0x101 * 0x201
        emulator.VeraFx.CacheWrite = true; // write to vram
        emulator.Vera.Data0_Address = 0x00;
        (new byte[] { 0xff, 0xff, 0xff, 0xff }).CopyTo(emulator.Vera.Vram.Slice(0));

        var (_, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                stp
                stz DATA0
                stp",
                emulator);

        snapshot.Snap();
        emulator.Emulate();

        snapshot.Compare()
                .IgnoreVia()
                .IgnoreVera()
                // 0x101 * 0x201 + 0x123 = 0x20402 + 0x123 = 0x20525
                .Is(MemoryAreas.Vram, 0x0000, new byte[] { 0x25, 0x05, 0x02, 0x00 })
                .AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task MultiplyWithAccumulatorSub()
    {
        var emulator = new Emulator();

        emulator.VeraFx.AccumulateDirection = 1;
        emulator.VeraFx.Accumulator = 0x40000;
        emulator.VeraFx.MultiplierEnable = true;
        emulator.VeraFx.Cache = 0x01010202; // 0x101 * 0x201
        emulator.VeraFx.CacheWrite = true; // write to vram
        emulator.Vera.Data0_Address = 0x00;
        (new byte[] { 0xff, 0xff, 0xff, 0xff }).CopyTo(emulator.Vera.Vram.Slice(0));

        var (_, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                stp
                stz DATA0
                stp",
                emulator);

        snapshot.Snap();
        emulator.Emulate();

        snapshot.Compare()
                .IgnoreVia()
                .IgnoreVera()
                // 0x40000 - 0x101 * 0x201 = 0x40000 - 0x20402 = 0x10402
                .Is(MemoryAreas.Vram, 0x0000, new byte[] { 0xfe, 0xfb, 0x01, 0x00 })
                .AssertNoOtherChanges();
    }
}