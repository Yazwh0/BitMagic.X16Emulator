﻿using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

[TestClass]
public class Interrupt_Vsync
{
    private const int VSYNC = 0x01;
    private const int AFLOW = 0x08;

    [TestMethod]
    public async Task Hit()
    {
        var emulator = new Emulator();

        emulator.InterruptHit = InterruptSource.None;
        emulator.InterruptMask = InterruptSource.None;

        emulator.RomBank[0x3ffe] = 0x00;
        emulator.RomBank[0x3fff] = 0x09;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #01
                sta IEN
                ldy #$ff
        .y_loop:
                ldx #$ff
        .x_loop:
                dex
                bne x_loop
                dey
                bne y_loop                

                stp
                .org $900
                stp",
                emulator);

        // emulation
        emulator.AssertState(Pc: 0x901);
        Assert.AreEqual(true, emulator.Vera.Interrupt_Vsync_Hit);
        Assert.AreEqual(false, emulator.Vera.Interrupt_Line_Hit);
        Assert.AreEqual(false, emulator.Vera.Interrupt_SpCol_Hit);
        Assert.AreEqual(VSYNC + AFLOW, emulator.Memory[0x9F27]);
        Assert.IsTrue(emulator.Vera.Beam_X <= 31);      // not 0 as the interrupt has to process + stp
        Assert.AreEqual(480, emulator.Vera.Beam_Y);     // will be on line 480
    }

    [TestMethod]
    public async Task Hit_Disabled()
    {
        var emulator = new Emulator();

        emulator.InterruptHit = InterruptSource.None;
        emulator.InterruptMask = InterruptSource.None;

        emulator.RomBank[0x3ffe] = 0x00;
        emulator.RomBank[0x3fff] = 0x09;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                sei
                lda #01
                sta IEN
                ldy #$ff
        .y_loop:
                ldx #$ff
        .x_loop:
                dex
                bne x_loop
                dey
                bne y_loop                

                stp
                .org $900
                stp",
                emulator);

        // emulation
        emulator.AssertState(Pc: 0x821);
        Assert.AreEqual(true, emulator.Vera.Interrupt_Vsync_Hit);
        Assert.AreEqual(false, emulator.Vera.Interrupt_Line_Hit);
        Assert.AreEqual(false, emulator.Vera.Interrupt_SpCol_Hit);
        Assert.AreEqual(VSYNC + AFLOW, emulator.Memory[0x9F27]);
    }

    [TestMethod]
    public async Task Hit_Reset()
    {
        var emulator = new Emulator();

        emulator.InterruptHit = InterruptSource.None;
        emulator.InterruptMask = InterruptSource.None;

        emulator.RomBank[0x3ffe] = 0x00;
        emulator.RomBank[0x3fff] = 0x09;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #01
                sta IEN
                ldy #$ff
        .y_loop:
                ldx #$ff
        .x_loop:
                dex
                bne x_loop
                dey
                bne y_loop                

                stp
                .org $900
                lda #01
                sta ISR
                stp",
                emulator);

        // emulation
        Assert.AreEqual(false, emulator.Vera.Interrupt_Vsync_Hit);
        Assert.AreEqual(false, emulator.Vera.Interrupt_Line_Hit);
        Assert.AreEqual(false, emulator.Vera.Interrupt_SpCol_Hit);
        Assert.AreEqual(AFLOW, emulator.Memory[0x9F27]);
        Assert.IsFalse(emulator.Interrupt);
    }


    [TestMethod]
    public async Task Hit_SetIen()
    {
        var emulator = new Emulator();

        emulator.InterruptHit = InterruptSource.None;
        emulator.InterruptMask = InterruptSource.None;

        emulator.RomBank[0x3ffe] = 0x00;
        emulator.RomBank[0x3fff] = 0x09;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #01
                sta IEN
                ldy #$ff
        .y_loop:
                ldx #$ff
        .x_loop:
                dex
                bne x_loop
                dey
                bne y_loop                

                stp
                .org $900
                stz IEN
                stp",
                emulator);

        // emulation
        Assert.AreEqual(true, emulator.Vera.Interrupt_Vsync_Hit);
        Assert.AreEqual(false, emulator.Vera.Interrupt_Line_Hit);
        Assert.AreEqual(false, emulator.Vera.Interrupt_SpCol_Hit);
        Assert.AreEqual(VSYNC + AFLOW, emulator.Memory[0x9F27]);
        Assert.IsFalse(emulator.Interrupt);
    }

    [TestMethod]
    public async Task Hit_SetIen_Return()
    {
        var emulator = new Emulator();

        emulator.InterruptHit = InterruptSource.None;
        emulator.InterruptMask = InterruptSource.None;

        emulator.RomBank[0x3ffe] = 0x00;
        emulator.RomBank[0x3fff] = 0x09;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #01
                sta IEN
                ldy #$ff
        .y_loop:
                ldx #$ff
        .x_loop:
                dex
                bne x_loop
                dey
                bne y_loop                

                stp
        .org $900
                lda #$ab
                stz IEN
                rti",
                emulator);

        emulator.AssertState(0xab);
        Assert.AreEqual(true, emulator.Vera.Interrupt_Vsync_Hit);
        Assert.AreEqual(false, emulator.Vera.Interrupt_Line_Hit);
        Assert.AreEqual(false, emulator.Vera.Interrupt_SpCol_Hit);
        Assert.AreEqual(VSYNC + AFLOW, emulator.Memory[0x9F27]);
        Assert.IsFalse(emulator.Interrupt);
    }

    [TestMethod]
    public async Task Hit_SetIen_Return_ReEnable()
    {
        var emulator = new Emulator();

        emulator.InterruptHit = InterruptSource.None;
        emulator.InterruptMask = InterruptSource.None;

        emulator.RomBank[0x3ffe] = 0x00;
        emulator.RomBank[0x3fff] = 0x09;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #01
                sta IEN
                ldy #$ff
        .y_loop:
                ldx #$ff
        .x_loop:
                dex
                bne x_loop
                dey
                bne y_loop                
            
                lda #01
                sta IEN

                stp
        .org $900
                lda #$ab
                stz IEN
                rti",
                emulator);

        emulator.AssertState(0xab);
        Assert.AreEqual(true, emulator.Vera.Interrupt_Vsync_Hit);
        Assert.AreEqual(false, emulator.Vera.Interrupt_Line_Hit);
        Assert.AreEqual(false, emulator.Vera.Interrupt_SpCol_Hit);
        Assert.AreEqual(VSYNC + AFLOW, emulator.Memory[0x9F27]);
        Assert.IsFalse(emulator.Interrupt);
    }

    [TestMethod]
    public async Task Hit_OnlyOnce_Clear()
    {
        var emulator = new Emulator();

        emulator.InterruptHit = InterruptSource.None;
        emulator.InterruptMask = InterruptSource.None;

        emulator.RomBank[0x3ffe] = 0x00;
        emulator.RomBank[0x3fff] = 0x09;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stz $03
                lda #01
                sta IEN
                ldy #$ff
        .y_loop:
                ldx #$ff
        .x_loop:
                dex
                bne x_loop
                dey
                bne y_loop                

                stp
                .org $900
                inc $03
                lda #01
                sta ISR
                stz IEN
                rti",
                emulator);

        // emulation
        Assert.AreEqual(0x01, emulator.Memory[0x03]);
        Assert.AreEqual(0x00, emulator.Memory[0x9F26]);
        Assert.AreEqual(AFLOW, emulator.Memory[0x9F27]);
        Assert.IsFalse(emulator.Interrupt);
    }

    [TestMethod]
    public async Task Hit_Many()
    {
        var emulator = new Emulator();

        emulator.InterruptHit = InterruptSource.None;
        emulator.InterruptMask = InterruptSource.None;

        emulator.RomBank[0x3ffe] = 0x00;
        emulator.RomBank[0x3fff] = 0x09;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                stz $03
                lda #01
                sta IEN
                ldy #$ff
        .y_loop:
                ldx #$ff
        .x_loop:
                dex
                bne x_loop
                dey
                bne y_loop                

                stp
                .org $900
                lda #01
                sta ISR
                inc $03 
                rti",
                emulator);

        // emulation
        Assert.AreEqual(0x02, emulator.Memory[0x03]);
    }
}