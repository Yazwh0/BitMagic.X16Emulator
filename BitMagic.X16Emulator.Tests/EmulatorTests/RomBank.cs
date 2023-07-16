using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Core;

[TestClass]
public class RomBank
{
    [TestMethod]
    public async Task Read()
    {
        var emulator = new Emulator();

        emulator.RomBank[0x4000] = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #$01
                sta $01
                ldx $c000
                stp",
                emulator);

        emulator.AssertState(X: 0xff);
    }

    [TestMethod]
    public async Task Read_253()
    {
        var emulator = new Emulator();

        emulator.RomBank[0x4000 * 0xfd] = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #$fd
                sta $01
                ldx $c000
                stp",
                emulator);

        emulator.AssertState(X: 0xff);
    }

    [TestMethod]
    public async Task Read_254()
    {
        var emulator = new Emulator();

        emulator.RomBank[0x4000 * 0xfe] = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #$fe
                sta $01
                ldx $c000
                stp",
                emulator);

        emulator.AssertState(X: 0xff);
    }

    [TestMethod]
    public async Task Read_255()
    {
        var emulator = new Emulator();

        emulator.RomBank[0x4000 * 0xff] = 0xff;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #$ff
                sta $01
                ldx $c000
                stp",
                emulator);

        emulator.AssertState(X: 0xff);
    }

    // Cartridge invalidates this test
    //[TestMethod]
    //public async Task ReadIgnoreHighBits()
    //{
    //    var emulator = new Emulator();

    //    emulator.RomBank[0x4000] = 0xff;

    //    await X16TestHelper.Emulate(@"
    //            .machine CommanderX16R40
    //            .org $810
    //            lda #$e1 ; still bank 1
    //            sta $01
    //            ldx $c000
    //            stp",
    //            emulator);

    //    emulator.AssertState(X: 0xff);
    //}
}