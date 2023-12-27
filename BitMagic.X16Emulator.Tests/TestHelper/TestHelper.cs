using BitMagic.X16Emulator.Snapshot;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.TestHelper;

[TestClass]
public class TestHelper
{
    [TestMethod]
    public async Task RamBankChanges()
    {
        var (emulator, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                lda #2
                sta RAM_BANK
                stp

                sta $a001
                stp");

        snapshot.Snap();

        emulator.Emulate();

        snapshot.Compare()
            .Is(X16Emulator.Snapshot.MemoryAreas.BankedRam, 0x02a001, 2)
            .IgnoreVera().IgnoreVia().AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task RamBankRangeChanges_RangeChange()
    {
        var (emulator, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                lda #2
                sta RAM_BANK
                stp

                sta $a001
                sta $a002
                sta $a003
                sta $a004
                sta $a005
                sta $a006
                sta $a007
                sta $a008
                stp");

        snapshot.Snap();

        emulator.Emulate();

        var changes = snapshot.Compare().IgnoreVera().IgnoreVia();

        var bankedChanges = changes.Changes.Select(i => i as MemoryRangeChange).Where(i => i != null && i.MemoryArea == MemoryAreas.BankedRam);

        Assert.AreEqual(1, bankedChanges.Count());
        var range = bankedChanges.First();
        Assert.IsNotNull(range);

        Assert.AreEqual(0x02 * 0x2000 + 0x1, range.Start);
        Assert.AreEqual(0x02 * 0x2000 + 0x8, range.End);

        snapshot.Compare()
            .CanChange(MemoryAreas.BankedRam, 0x02a001, 0x02a008)
            .IgnoreVera().IgnoreVia().AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task RamBankRangeChanges_ValueChange()
    {
        var (emulator, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                lda #2
                sta RAM_BANK
                stp

                sta $a001
                sta $a002
                sta $a003
                sta $a004
                sta $a005
                sta $a006
                sta $a007
                sta $a008
                stp");

        snapshot.Snap();

        emulator.Emulate();

        snapshot.Compare()
            .CanChange(MemoryAreas.BankedRam, 0x02a001)
            .CanChange(MemoryAreas.BankedRam, 0x02a002)
            .CanChange(MemoryAreas.BankedRam, 0x02a003)
            .CanChange(MemoryAreas.BankedRam, 0x02a004)
            .CanChange(MemoryAreas.BankedRam, 0x02a005)
            .CanChange(MemoryAreas.BankedRam, 0x02a006)
            .CanChange(MemoryAreas.BankedRam, 0x02a007)
            .CanChange(MemoryAreas.BankedRam, 0x02a008)
            .IgnoreVera().IgnoreVia().AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task RamBankRangeChanges_ValueChangeMissing()
    {
        var (emulator, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                lda #2
                sta RAM_BANK
                stp

                sta $a001
                sta $a002
                sta $a003
                sta $a004
                sta $a005
                sta $a006
                sta $a007
                sta $a008
                stp");

        snapshot.Snap();

        emulator.Emulate();

        bool exception = false;
        try
        {
            snapshot.Compare()
                .CanChange(MemoryAreas.BankedRam, 0x02a001)
                .CanChange(MemoryAreas.BankedRam, 0x02a002)
                .CanChange(MemoryAreas.BankedRam, 0x02a003)
                .CanChange(MemoryAreas.BankedRam, 0x02a004)
                .CanChange(MemoryAreas.BankedRam, 0x02a006)
                .CanChange(MemoryAreas.BankedRam, 0x02a007)
                .CanChange(MemoryAreas.BankedRam, 0x02a008)
                .IgnoreVera().IgnoreVia().AssertNoOtherChanges();
        } 
        catch(AssertFailedException)
        {
            exception = true;
        }

        Assert.IsTrue(exception);
    }

    [TestMethod]
    public async Task RamBankRangeChanges_ValueChangeHole()
    {
        var (emulator, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                lda #2
                sta RAM_BANK
                stp

                sta $a001
                sta $a002
                sta $a004
                stp");

        snapshot.Snap();

        emulator.Emulate();

        snapshot.Compare()
            .CanChange(MemoryAreas.BankedRam, 0x02a001)
            .CanChange(MemoryAreas.BankedRam, 0x02a002)
            .CanChange(MemoryAreas.BankedRam, 0x02a004)
            .IgnoreVera().IgnoreVia().AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task RamBankRangeChanges_WithHoles()
    {
        var (emulator, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                lda #2
                sta RAM_BANK
                stp

                sta $a001
                sta $a002
                sta $a003
                sta $a004
                sta $a007
                sta $a008
                sta $a009
                sta $a00a
                stp");

        snapshot.Snap();

        emulator.Emulate();

        var changes = snapshot.Compare().IgnoreVera().IgnoreVia();

        var bankedChanges = changes.Changes.Select(i => i as MemoryRangeChange).Where(i => i != null && i.MemoryArea == MemoryAreas.BankedRam);

        Assert.AreEqual(1, bankedChanges.Count());
        var range = bankedChanges.First();
        Assert.IsNotNull(range);

        Assert.AreEqual(0x02 * 0x2000 + 0x1, range.Start);
        Assert.AreEqual(0x02 * 0x2000 + 0xa, range.End);

        snapshot.Compare()
            .CanChange(MemoryAreas.BankedRam, 0x02a001, 0x02a00a)
            .IgnoreVera().IgnoreVia().AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task RamBankRangeChanges_WithHoles_CanChangeValue()
    {
        var (emulator, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                lda #2
                sta RAM_BANK
                stp

                sta $a001
                sta $a002
                sta $a003
                sta $a004
                sta $a007
                sta $a008
                sta $a009
                sta $a00a
                stp");

        snapshot.Snap();

        emulator.Emulate();

        snapshot.Compare()
            .CanChange(MemoryAreas.BankedRam, 0x02a001, 0x02a004)
            .CanChange(MemoryAreas.BankedRam, 0x02a007, 0x02a00a)
            .IgnoreVera().IgnoreVia().AssertNoOtherChanges();
    }

    [TestMethod]
    public async Task RamBankRangeChanges_WithHoles_CanChangeValue_OneOffEnd()
    {
        var (emulator, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                lda #2
                sta RAM_BANK
                stp

                sta $a001
                sta $a002
                sta $a003
                sta $a004
                sta $a007
                sta $a008
                sta $a009
                sta $a00a
                sta $a00b
                stp");

        snapshot.Snap();

        emulator.Emulate();

        bool exception = false;
        try
        {
            snapshot.Compare()
                .CanChange(MemoryAreas.BankedRam, 0x02a001, 0x02a004)
                .CanChange(MemoryAreas.BankedRam, 0x02a007, 0x02a00a)
                .IgnoreVera().IgnoreVia().AssertNoOtherChanges();
        }
        catch (AssertFailedException)
        {
            exception = true;
        }

        Assert.IsTrue(exception);
    }

    [TestMethod]
    public async Task RamBankRangeChanges_WithHoles_CanChangeValue_OneOffStart()
    {
        var (emulator, snapshot) = await X16TestHelper.EmulateChanges(@"
                .machine CommanderX16R40
                .org $810
                lda #2
                sta RAM_BANK
                stp

                sta $a000
                sta $a001
                sta $a002
                sta $a003
                sta $a004
                sta $a007
                sta $a008
                sta $a009
                sta $a00a
                stp");

        snapshot.Snap();

        emulator.Emulate();

        bool exception = false;
        try
        {
            snapshot.Compare()
                .CanChange(MemoryAreas.BankedRam, 0x02a001, 0x02a004)
                .CanChange(MemoryAreas.BankedRam, 0x02a007, 0x02a00a)
                .IgnoreVera().IgnoreVia().AssertNoOtherChanges();
        }
        catch (AssertFailedException)
        {
            exception = true;
        }

        Assert.IsTrue(exception);
    }
}