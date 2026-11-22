using Microsoft.VisualStudio.TestTools.UnitTesting;
using BitMagic.X16Emulator.Snapshot;

namespace BitMagic.X16Emulator.Tests.Vera.Fx;

[TestClass]
public class LineDrawHelper
{
    [TestMethod]
    public async Task SetXIncr_Bottom()
    {
        using var emulator = new Emulator();

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
        emulator.EmulateWithTimeout();

        snapshot.Compare().IgnoreVia()
            .IgnoreVera()
            .AssertNoOtherChanges();

        Assert.AreEqual(0x00007f80u, emulator.VeraFx.IncrementX); // 6.9 fixed point, stored as 16.16
    }

    [TestMethod]
    public async Task SetXIncr_Top()
    {
        using var emulator = new Emulator();

        emulator.Vera.DcSel = 0x03;
        emulator.VeraFx.AddrMode = 1; // line draw (2 is polygon fill)
        emulator.A = 0x7f;

        var (_, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                stp
                sta FX_X_INCR_H
                stp",
                emulator);

        snapshot.Snap();
        emulator.EmulateWithTimeout();

        snapshot.Compare().IgnoreVia()
            .IgnoreVera()
            .AssertNoOtherChanges();

        Assert.AreEqual(0xffff8000u, emulator.VeraFx.IncrementX); // $7f00 is -$100 (6.9 signed), stored as 16.16
        Assert.AreEqual(0x00008000u, emulator.VeraFx.PositionX);
        Assert.IsFalse(emulator.VeraFx.Mult32X);
    }

    [TestMethod]
    public async Task SetXIncr_Top_Set23x()
    {
        using var emulator = new Emulator();

        emulator.Vera.DcSel = 0x03;
        emulator.VeraFx.AddrMode = 1; // line draw (2 is polygon fill)
        emulator.A = 0xff;

        var (_, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                stp
                sta FX_X_INCR_H
                stp",
                emulator);

        snapshot.Snap();
        emulator.EmulateWithTimeout();

        snapshot.Compare().IgnoreVia()
            .IgnoreVera()
            .AssertNoOtherChanges();

        Assert.AreEqual(0xfff00000u, emulator.VeraFx.IncrementX); // -$100 (6.9 signed) x32, stored as 16.16
        Assert.AreEqual(0x00008000u, emulator.VeraFx.PositionX);
        Assert.IsTrue(emulator.VeraFx.Mult32X);
    }

    [TestMethod]
    public async Task SetXIncr_Top_NotLineDraw()
    {
        using var emulator = new Emulator();

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
        emulator.EmulateWithTimeout();

        snapshot.Compare().IgnoreVia()
            .IgnoreVera()
            .AssertNoOtherChanges();

        Assert.AreEqual(0xffff8000u, emulator.VeraFx.IncrementX); // $7f00 is -$100 (6.9 signed), stored as 16.16
        Assert.AreEqual(0x00008000u, emulator.VeraFx.PositionX); // unchanged, it starts at half a pixel
        Assert.IsFalse(emulator.VeraFx.Mult32X);
    }
}