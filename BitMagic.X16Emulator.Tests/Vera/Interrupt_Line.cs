﻿using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests;

[TestClass]
public class Interrupt_Line
{
    private const int LINE = 0x02;
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
                lda #100             ; line 100
                sta IRQLINE_L
                lda #02
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
        Assert.AreEqual(false, emulator.Vera.Interrupt_Vsync_Hit);
        Assert.AreEqual(true, emulator.Vera.Interrupt_Line_Hit);
        Assert.AreEqual(false, emulator.Vera.Interrupt_SpCol_Hit);
        Assert.AreEqual(LINE + AFLOW, emulator.Memory[0x9F27]);
        Assert.IsTrue(emulator.Vera.Beam_X <= 31); // can vary, but should be close to the start
        Assert.AreEqual(100, emulator.Vera.Beam_Y);
    }

    [TestMethod]
    public async Task Hit_Wai()
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
                lda #100             ; line 100
                sta IRQLINE_L
                lda #02
                sta IEN
                wai            

                stp",
                emulator);

        // emulation
        Assert.AreEqual(false, emulator.Vera.Interrupt_Vsync_Hit);
        Assert.AreEqual(true, emulator.Vera.Interrupt_Line_Hit);
        Assert.AreEqual(false, emulator.Vera.Interrupt_SpCol_Hit);
        Assert.AreEqual(LINE + AFLOW, emulator.Memory[0x9F27]);
        Assert.IsTrue(emulator.Vera.Beam_X <= 31); // can vary, but should be close to the start
        Assert.AreEqual(100, emulator.Vera.Beam_Y);
    }

    [TestMethod]
    public async Task Hit_Wai_Twice()
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
                lda #100             ; line 100
                sta IRQLINE_L
                lda #02
                sta IEN
                wai            
                sta ISR
                wai
                stp",
                emulator);

        // emulation
        Assert.AreEqual(false, emulator.Vera.Interrupt_Vsync_Hit);
        Assert.AreEqual(true, emulator.Vera.Interrupt_Line_Hit);
        Assert.AreEqual(false, emulator.Vera.Interrupt_SpCol_Hit);
        Assert.AreEqual(LINE + AFLOW, emulator.Memory[0x9F27]);
        Assert.IsTrue(emulator.Vera.Beam_X <= 31); // can vary, but should be close to the start
        Assert.AreEqual(100, emulator.Vera.Beam_Y);
    }

    [TestMethod]
    public async Task Hit_Line0()
    {
        var emulator = new Emulator();

        emulator.InterruptHit = InterruptSource.None;
        emulator.InterruptMask = InterruptSource.None;

        emulator.RomBank[0x3ffe] = 0x00;
        emulator.RomBank[0x3fff] = 0x09;

        await X16TestHelper.Emulate(@"
                .machine CommanderX16R40
                .org $810
                lda #0             ; line 0
                sta IRQLINE_L
                lda #02
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
        Assert.AreEqual(false, emulator.Vera.Interrupt_Vsync_Hit);
        Assert.AreEqual(true, emulator.Vera.Interrupt_Line_Hit);
        Assert.AreEqual(false, emulator.Vera.Interrupt_SpCol_Hit);
        Assert.AreEqual(LINE + AFLOW, emulator.Memory[0x9F27]);
        //Assert.AreEqual(0, emulator.Vera.Beam_X); cant check as interrupt hits mid instruction
        Assert.AreEqual(0, emulator.Vera.Beam_Y);
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
                lda #100             ; line 100
                sta IRQLINE_L
                lda #02

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
                lda #02
                sta ISR
                stp",
                emulator);

        // emulation
        Assert.AreEqual(false, emulator.Vera.Interrupt_Vsync_Hit);
        Assert.AreEqual(false, emulator.Vera.Interrupt_Line_Hit);
        Assert.AreEqual(false, emulator.Vera.Interrupt_SpCol_Hit);
        Assert.AreEqual(AFLOW, emulator.Memory[0x9F27]);
    }

    [TestMethod]
    public async Task Hit_OnlyOnce()
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
                lda #100             ; line 100
                sta IRQLINE_L
                lda #02
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
                lda #02
                sta ISR
                stz IEN
                
                rti",
                emulator);

        // emulation
        Assert.AreEqual(0x01, emulator.Memory[0x03]);
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
                lda #100             ; line 100
                sta IRQLINE_L
                lda #02
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
                lda #02
                sta ISR
                inc $03 
                rti",
                emulator);

        // emulation
        Assert.AreEqual(0x03, emulator.Memory[0x03]);
    }
}