using Microsoft.VisualStudio.TestTools.UnitTesting;
using BitMagic.X16Emulator.Snapshot;

namespace BitMagic.X16Emulator.Tests.Vera.Fx;

[TestClass]
public class LineDrawHelper
{
    [TestMethod]
    public async Task SetXIncr_Bottom()
    {
        var emulator = new Emulator();

        emulator.Vera.DcSel = 0x03;
        emulator.A = 0xff;

        var (_, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                stp
                sta FX_X_INCR_L
                stp",
                emulator);

        snapshot.Snap();
        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .IgnoreVera()
            .AssertNoOtherChanges();

        Assert.AreEqual(0x00003fc0u, emulator.VeraFx.IncrementX);
    }

    [TestMethod]
    public async Task SetXIncr_Top()
    {
        var emulator = new Emulator();

        emulator.Vera.DcSel = 0x03;
        emulator.VeraFx.AddrMode = 2; // line draw
        emulator.A = 0x7f;

        var (_, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                stp
                sta FX_X_INCR_H
                stp",
                emulator);

        snapshot.Snap();
        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .IgnoreVera()
            .AssertNoOtherChanges();

        Assert.AreEqual(0x001fc000u, emulator.VeraFx.IncrementX);
        Assert.AreEqual(0x00008000u, emulator.VeraFx.PositionX);
        Assert.IsFalse(emulator.VeraFx.Mult32X);
    }

    [TestMethod]
    public async Task SetXIncr_Top_Set23x()
    {
        var emulator = new Emulator();

        emulator.Vera.DcSel = 0x03;
        emulator.VeraFx.AddrMode = 2; // line draw
        emulator.A = 0xff;

        var (_, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                stp
                sta FX_X_INCR_H
                stp",
                emulator);

        snapshot.Snap();
        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .IgnoreVera()
            .AssertNoOtherChanges();

        Assert.AreEqual(0x001fc000u, emulator.VeraFx.IncrementX);
        Assert.AreEqual(0x00008000u, emulator.VeraFx.PositionX);
        Assert.IsTrue(emulator.VeraFx.Mult32X);
    }

    [TestMethod]
    public async Task SetXIncr_Top_NotLineDraw()
    {
        var emulator = new Emulator();

        emulator.Vera.DcSel = 0x03;
        emulator.VeraFx.AddrMode = 0; // Not line draw
        emulator.A = 0x7f;

        var (_, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                stp
                sta FX_X_INCR_H
                stp",
                emulator);

        snapshot.Snap();
        emulator.Emulate();

        snapshot.Compare().IgnoreVia()
            .IgnoreVera()
            .AssertNoOtherChanges();

        Assert.AreEqual(0x001fc000u, emulator.VeraFx.IncrementX);
        Assert.AreEqual(0x00000000u, emulator.VeraFx.PositionX);
        Assert.IsFalse(emulator.VeraFx.Mult32X);
    }
}