﻿using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BitMagic.X16Emulator.Tests.Smc;

[TestClass]
public class SmcWrite
{
    [TestMethod]
    public async Task PowerOff()
    {
        var emulator = new Emulator();

        await X16TestHelper.Emulate(@"
            .machine CommanderX16R42
            .org $810
            ; DDRA is data direction.
            ; PRA is the data

            .const SDA = 1
            .const SCL = 2

                lda #$00
                ldx #$42
                ldy #$01

                jsr write_first_byte

                jsr i2c_write_stop

                stp

            ;---------------------------------------------------------------
            ; i2c_write_first_byte
            ; 
            ; Function: Writes one byte over I2C without stopping the
            ;           transmission. Subsequent bytes may be written by
            ;           i2c_write_next_byte. When done, call function
            ;           i2c_write_stop to close the I2C transmission.
            ;
            ; Pass:      a    value
            ;            x    7-bit device address
            ;            y    offset
            ;
            ; Return:    c    1 on error (NAK)
            ;---------------------------------------------------------------
            .proc write_first_byte
                pha                ; value
                jsr i2c_init
                jsr i2c_start
                txa                ; device
                asl
                phy
                jsr i2c_write
                ply
                bcs error
                tya                ; offset
                phy
                jsr i2c_write
                ply
                pla                ; value
                jsr i2c_write
                clc
                rts

            .error:
                pla                ; value
                sec
                rts
            .endproc

            ;---------------------------------------------------------------
            ; i2c_write_next_byte
            ;
            ; Function:	After the first byte has been written by 
            ;			i2c_write_first_byte, this function may be used to
            ;			write one or more subsequent bytes without 
            ;			restarting the I2C transmission
            ;
            ; Pass:		a    value
            ;
            ; Return:	Nothing
            ;---------------------------------------------------------------
            .proc i2c_write_next_byte
                jmp i2c_write
            .endproc

            ;---------------------------------------------------------------
            ; i2c_write
            ;
            ; Function: Write a single byte over I2C
            ;
            ; Pass:      a    byte to write
            ;
            ; Return:    c    0 if ACK, 1 if NAK
            ;
            ; I2C Exit:  SDA: Z
            ;            SCL: 0
            ;---------------------------------------------------------------
            .proc i2c_write
                ldx #8
            .i2c_write_loop:
                rol
                tay
                jsr send_bit
                tya
                dex
                bne i2c_write_loop
                jsr rec_bit     ; C = 0: success
                rts
            .endproc

            ;---------------------------------------------------------------
            ; i2c_write_stop
            ;
            ; Function:	Stops I2C transmission that has been initialized
            ;			with i2c_write_first_byte
            ;
            ; Pass:		Nothing
            ;
            ; Return:	Nothing
            ;---------------------------------------------------------------
            .proc i2c_write_stop
                jsr i2c_stop
            .endproc


            .proc i2c_start
                jsr sda_low
                jsr i2c_brief_delay
                jsr scl_low
                rts
            .endproc

            .proc i2c_stop
                jsr sda_low
                jsr i2c_brief_delay
                jsr scl_high
                jsr i2c_brief_delay
                jsr sda_high
                jsr i2c_brief_delay
                rts
            .endproc

            .proc sda_low
                lda #SDA
                tsb V_DDRA
                rts
            .endproc

            .proc sda_high
                lda #SDA
                trb V_DDRA
                rts
            .endproc

            .proc scl_low
                lda #SCL
                tsb V_DDRA
                rts
            .endproc

            .proc scl_high
                lda #SCL
                trb V_DDRA
            .loop:
                lda V_PRA     ; Wait for clock to go high
                and #SCL
                beq loop
                rts
            .endproc

            .proc send_bit
                bcs high
                jsr sda_low
                bra done
            .high:
                jsr sda_high
            .done:
                jsr scl_high
                jsr scl_low
                rts
            .endproc

            .proc i2c_init
                lda #SDA | SCL
                trb V_PRA
                jsr sda_high
                jsr scl_high
                rts
            .endproc

            .proc rec_bit
                jsr sda_high		; Release SDA so that device can drive it
                jsr scl_high
                lda V_PRA
                lsr             ; bit -> C
                jsr scl_low
                rts
            .endproc

            .proc i2c_brief_delay
                nop
                nop
                nop
                nop
                rts
            .endproc",
                emulator, expectedResult: Emulator.EmulatorResult.SmcPowerOff);

        Assert.AreEqual((uint)1, emulator.Smc.Offset);
    }

    [TestMethod]
    public async Task HardReset()
    {
        var emulator = new Emulator();

        await X16TestHelper.Emulate(@"
            .machine CommanderX16R42
            .org $810
            ; DDRA is data direction.
            ; PRA is the data

            .const SDA = 1
            .const SCL = 2

                lda #$01
                ldx #$42
                ldy #$01

                jsr write_first_byte

                jsr i2c_write_stop

                stp

            ;---------------------------------------------------------------
            ; i2c_write_first_byte
            ; 
            ; Function: Writes one byte over I2C without stopping the
            ;           transmission. Subsequent bytes may be written by
            ;           i2c_write_next_byte. When done, call function
            ;           i2c_write_stop to close the I2C transmission.
            ;
            ; Pass:      a    value
            ;            x    7-bit device address
            ;            y    offset
            ;
            ; Return:    c    1 on error (NAK)
            ;---------------------------------------------------------------
            .proc write_first_byte
                pha                ; value
                jsr i2c_init
                jsr i2c_start
                txa                ; device
                asl
                phy
                jsr i2c_write
                ply
                bcs error
                tya                ; offset
                phy
                jsr i2c_write
                ply
                pla                ; value
                jsr i2c_write
                clc
                rts

            .error:
                pla                ; value
                sec
                rts
            .endproc

            ;---------------------------------------------------------------
            ; i2c_write_next_byte
            ;
            ; Function:	After the first byte has been written by 
            ;			i2c_write_first_byte, this function may be used to
            ;			write one or more subsequent bytes without 
            ;			restarting the I2C transmission
            ;
            ; Pass:		a    value
            ;
            ; Return:	Nothing
            ;---------------------------------------------------------------
            .proc i2c_write_next_byte
                jmp i2c_write
            .endproc

            ;---------------------------------------------------------------
            ; i2c_write
            ;
            ; Function: Write a single byte over I2C
            ;
            ; Pass:      a    byte to write
            ;
            ; Return:    c    0 if ACK, 1 if NAK
            ;
            ; I2C Exit:  SDA: Z
            ;            SCL: 0
            ;---------------------------------------------------------------
            .proc i2c_write
                ldx #8
            .i2c_write_loop:
                rol
                tay
                jsr send_bit
                tya
                dex
                bne i2c_write_loop
                jsr rec_bit     ; C = 0: success
                rts
            .endproc

            ;---------------------------------------------------------------
            ; i2c_write_stop
            ;
            ; Function:	Stops I2C transmission that has been initialized
            ;			with i2c_write_first_byte
            ;
            ; Pass:		Nothing
            ;
            ; Return:	Nothing
            ;---------------------------------------------------------------
            .proc i2c_write_stop
                jsr i2c_stop
            .endproc


            .proc i2c_start
                jsr sda_low
                jsr i2c_brief_delay
                jsr scl_low
                rts
            .endproc

            .proc i2c_stop
                jsr sda_low
                jsr i2c_brief_delay
                jsr scl_high
                jsr i2c_brief_delay
                jsr sda_high
                jsr i2c_brief_delay
                rts
            .endproc

            .proc sda_low
                lda #SDA
                tsb V_DDRA
                rts
            .endproc

            .proc sda_high
                lda #SDA
                trb V_DDRA
                rts
            .endproc

            .proc scl_low
                lda #SCL
                tsb V_DDRA
                rts
            .endproc

            .proc scl_high
                lda #SCL
                trb V_DDRA
            .loop:
                lda V_PRA     ; Wait for clock to go high
                and #SCL
                beq loop
                rts
            .endproc

            .proc send_bit
                bcs high
                jsr sda_low
                bra done
            .high:
                jsr sda_high
            .done:
                jsr scl_high
                jsr scl_low
                rts
            .endproc

            .proc i2c_init
                lda #SDA | SCL
                trb V_PRA
                jsr sda_high
                jsr scl_high
                rts
            .endproc

            .proc rec_bit
                jsr sda_high		; Release SDA so that device can drive it
                jsr scl_high
                lda V_PRA
                lsr             ; bit -> C
                jsr scl_low
                rts
            .endproc

            .proc i2c_brief_delay
                nop
                nop
                nop
                nop
                rts
            .endproc",
                emulator, expectedResult: Emulator.EmulatorResult.SmcReset);

        Assert.AreEqual((uint)1, emulator.Smc.Offset);
    }

    [TestMethod]
    public async Task Reset()
    {
        var emulator = new Emulator();

        await X16TestHelper.Emulate(@"
            .machine CommanderX16R42
            .org $810
            ; DDRA is data direction.
            ; PRA is the data

            .const SDA = 1
            .const SCL = 2

                lda #$00
                ldx #$42
                ldy #$02

                jsr write_first_byte

                jsr i2c_write_stop

                stp

            ;---------------------------------------------------------------
            ; i2c_write_first_byte
            ; 
            ; Function: Writes one byte over I2C without stopping the
            ;           transmission. Subsequent bytes may be written by
            ;           i2c_write_next_byte. When done, call function
            ;           i2c_write_stop to close the I2C transmission.
            ;
            ; Pass:      a    value
            ;            x    7-bit device address
            ;            y    offset
            ;
            ; Return:    c    1 on error (NAK)
            ;---------------------------------------------------------------
            .proc write_first_byte
                pha                ; value
                jsr i2c_init
                jsr i2c_start
                txa                ; device
                asl
                phy
                jsr i2c_write
                ply
                bcs error
                tya                ; offset
                phy
                jsr i2c_write
                ply
                pla                ; value
                jsr i2c_write
                clc
                rts

            .error:
                pla                ; value
                sec
                rts
            .endproc

            ;---------------------------------------------------------------
            ; i2c_write_next_byte
            ;
            ; Function:	After the first byte has been written by 
            ;			i2c_write_first_byte, this function may be used to
            ;			write one or more subsequent bytes without 
            ;			restarting the I2C transmission
            ;
            ; Pass:		a    value
            ;
            ; Return:	Nothing
            ;---------------------------------------------------------------
            .proc i2c_write_next_byte
                jmp i2c_write
            .endproc

            ;---------------------------------------------------------------
            ; i2c_write
            ;
            ; Function: Write a single byte over I2C
            ;
            ; Pass:      a    byte to write
            ;
            ; Return:    c    0 if ACK, 1 if NAK
            ;
            ; I2C Exit:  SDA: Z
            ;            SCL: 0
            ;---------------------------------------------------------------
            .proc i2c_write
                ldx #8
            .i2c_write_loop:
                rol
                tay
                jsr send_bit
                tya
                dex
                bne i2c_write_loop
                jsr rec_bit     ; C = 0: success
                rts
            .endproc

            ;---------------------------------------------------------------
            ; i2c_write_stop
            ;
            ; Function:	Stops I2C transmission that has been initialized
            ;			with i2c_write_first_byte
            ;
            ; Pass:		Nothing
            ;
            ; Return:	Nothing
            ;---------------------------------------------------------------
            .proc i2c_write_stop
                jsr i2c_stop
            .endproc


            .proc i2c_start
                jsr sda_low
                jsr i2c_brief_delay
                jsr scl_low
                rts
            .endproc

            .proc i2c_stop
                jsr sda_low
                jsr i2c_brief_delay
                jsr scl_high
                jsr i2c_brief_delay
                jsr sda_high
                jsr i2c_brief_delay
                rts
            .endproc

            .proc sda_low
                lda #SDA
                tsb V_DDRA
                rts
            .endproc

            .proc sda_high
                lda #SDA
                trb V_DDRA
                rts
            .endproc

            .proc scl_low
                lda #SCL
                tsb V_DDRA
                rts
            .endproc

            .proc scl_high
                lda #SCL
                trb V_DDRA
            .loop:
                lda V_PRA     ; Wait for clock to go high
                and #SCL
                beq loop
                rts
            .endproc

            .proc send_bit
                bcs high
                jsr sda_low
                bra done
            .high:
                jsr sda_high
            .done:
                jsr scl_high
                jsr scl_low
                rts
            .endproc

            .proc i2c_init
                lda #SDA | SCL
                trb V_PRA
                jsr sda_high
                jsr scl_high
                rts
            .endproc

            .proc rec_bit
                jsr sda_high		; Release SDA so that device can drive it
                jsr scl_high
                lda V_PRA
                lsr             ; bit -> C
                jsr scl_low
                rts
            .endproc

            .proc i2c_brief_delay
                nop
                nop
                nop
                nop
                rts
            .endproc",
                emulator, expectedResult: Emulator.EmulatorResult.SmcReset);

        Assert.AreEqual((uint)2, emulator.Smc.Offset);
    }

    [TestMethod]
    public async Task Led()
    {
        var emulator = new Emulator();

        await X16TestHelper.Emulate(@"
            .machine CommanderX16R42
            .org $810
            ; DDRA is data direction.
            ; PRA is the data

            .const SDA = 1
            .const SCL = 2

                lda #$ab
                ldx #$42
                ldy #$05

                jsr write_first_byte

                jsr i2c_write_stop

                stp

            ;---------------------------------------------------------------
            ; i2c_write_first_byte
            ; 
            ; Function: Writes one byte over I2C without stopping the
            ;           transmission. Subsequent bytes may be written by
            ;           i2c_write_next_byte. When done, call function
            ;           i2c_write_stop to close the I2C transmission.
            ;
            ; Pass:      a    value
            ;            x    7-bit device address
            ;            y    offset
            ;
            ; Return:    c    1 on error (NAK)
            ;---------------------------------------------------------------
            .proc write_first_byte
                pha                ; value
                jsr i2c_init
                jsr i2c_start
                txa                ; device
                asl
                phy
                jsr i2c_write
                ply
                bcs error
                tya                ; offset
                phy
                jsr i2c_write
                ply
                pla                ; value
                jsr i2c_write
                clc
                rts

            .error:
                pla                ; value
                sec
                rts
            .endproc

            ;---------------------------------------------------------------
            ; i2c_write_next_byte
            ;
            ; Function:	After the first byte has been written by 
            ;			i2c_write_first_byte, this function may be used to
            ;			write one or more subsequent bytes without 
            ;			restarting the I2C transmission
            ;
            ; Pass:		a    value
            ;
            ; Return:	Nothing
            ;---------------------------------------------------------------
            .proc i2c_write_next_byte
                jmp i2c_write
            .endproc

            ;---------------------------------------------------------------
            ; i2c_write
            ;
            ; Function: Write a single byte over I2C
            ;
            ; Pass:      a    byte to write
            ;
            ; Return:    c    0 if ACK, 1 if NAK
            ;
            ; I2C Exit:  SDA: Z
            ;            SCL: 0
            ;---------------------------------------------------------------
            .proc i2c_write
                ldx #8
            .i2c_write_loop:
                rol
                tay
                jsr send_bit
                tya
                dex
                bne i2c_write_loop
                jsr rec_bit     ; C = 0: success
                rts
            .endproc

            ;---------------------------------------------------------------
            ; i2c_write_stop
            ;
            ; Function:	Stops I2C transmission that has been initialized
            ;			with i2c_write_first_byte
            ;
            ; Pass:		Nothing
            ;
            ; Return:	Nothing
            ;---------------------------------------------------------------
            .proc i2c_write_stop
                jsr i2c_stop
            .endproc


            .proc i2c_start
                jsr sda_low
                jsr i2c_brief_delay
                jsr scl_low
                rts
            .endproc

            .proc i2c_stop
                jsr sda_low
                jsr i2c_brief_delay
                jsr scl_high
                jsr i2c_brief_delay
                jsr sda_high
                jsr i2c_brief_delay
                rts
            .endproc

            .proc sda_low
                lda #SDA
                tsb V_DDRA
                rts
            .endproc

            .proc sda_high
                lda #SDA
                trb V_DDRA
                rts
            .endproc

            .proc scl_low
                lda #SCL
                tsb V_DDRA
                rts
            .endproc

            .proc scl_high
                lda #SCL
                trb V_DDRA
            .loop:
                lda V_PRA     ; Wait for clock to go high
                and #SCL
                beq loop
                rts
            .endproc

            .proc send_bit
                bcs high
                jsr sda_low
                bra done
            .high:
                jsr sda_high
            .done:
                jsr scl_high
                jsr scl_low
                rts
            .endproc

            .proc i2c_init
                lda #SDA | SCL
                trb V_PRA
                jsr sda_high
                jsr scl_high
                rts
            .endproc

            .proc rec_bit
                jsr sda_high		; Release SDA so that device can drive it
                jsr scl_high
                lda V_PRA
                lsr             ; bit -> C
                jsr scl_low
                rts
            .endproc

            .proc i2c_brief_delay
                nop
                nop
                nop
                nop
                rts
            .endproc",
                emulator);

        Assert.AreEqual((uint)5, emulator.Smc.Offset);
        Assert.AreEqual((uint)0xab, emulator.Smc.Led);
    }


    [TestMethod]
    public async Task Data_2Bytes()
    {
        var emulator = new Emulator();

        await X16TestHelper.Emulate(@"
            .machine CommanderX16R42
            .org $810
            ; DDRA is data direction.
            ; PRA is the data

            .const SDA = 1
            .const SCL = 2

                lda #$cd
                ldx #$42
                ldy #$ab

                jsr write_first_byte

                stp

            ;---------------------------------------------------------------
            ; i2c_write_first_byte
            ; 
            ; Function: Writes one byte over I2C without stopping the
            ;           transmission. Subsequent bytes may be written by
            ;           i2c_write_next_byte. When done, call function
            ;           i2c_write_stop to close the I2C transmission.
            ;
            ; Pass:      a    value
            ;            x    7-bit device address
            ;            y    offset
            ;
            ; Return:    c    1 on error (NAK)
            ;---------------------------------------------------------------
            .proc write_first_byte
                pha                ; value
                jsr i2c_init
                jsr i2c_start
                txa                ; device
                asl
                phy
                jsr i2c_write
                ply
                bcs error
                tya                ; offset
                phy
                jsr i2c_write
                ply
                pla                ; value
                jsr i2c_write
                clc
                rts

            .error:
                pla                ; value
                sec
                rts
            .endproc

            ;---------------------------------------------------------------
            ; i2c_write_next_byte
            ;
            ; Function:	After the first byte has been written by 
            ;			i2c_write_first_byte, this function may be used to
            ;			write one or more subsequent bytes without 
            ;			restarting the I2C transmission
            ;
            ; Pass:		a    value
            ;
            ; Return:	Nothing
            ;---------------------------------------------------------------
            .proc i2c_write_next_byte
                jmp i2c_write
            .endproc

            ;---------------------------------------------------------------
            ; i2c_write
            ;
            ; Function: Write a single byte over I2C
            ;
            ; Pass:      a    byte to write
            ;
            ; Return:    c    0 if ACK, 1 if NAK
            ;
            ; I2C Exit:  SDA: Z
            ;            SCL: 0
            ;---------------------------------------------------------------
            .proc i2c_write
                ldx #8
            .i2c_write_loop:
                rol
                tay
                jsr send_bit
                tya
                dex
                bne i2c_write_loop
                jsr rec_bit     ; C = 0: success
                rts
            .endproc

            ;---------------------------------------------------------------
            ; i2c_write_stop
            ;
            ; Function:	Stops I2C transmission that has been initialized
            ;			with i2c_write_first_byte
            ;
            ; Pass:		Nothing
            ;
            ; Return:	Nothing
            ;---------------------------------------------------------------
            .proc i2c_write_stop
                jsr i2c_stop
            .endproc


            .proc i2c_start
                jsr sda_low
                jsr i2c_brief_delay
                jsr scl_low
                rts
            .endproc

            .proc i2c_stop
                jsr sda_low
                jsr i2c_brief_delay
                jsr scl_high
                jsr i2c_brief_delay
                jsr sda_high
                jsr i2c_brief_delay
                rts
            .endproc

            .proc sda_low
                lda #SDA
                tsb V_DDRA
                rts
            .endproc

            .proc sda_high
                lda #SDA
                trb V_DDRA
                rts
            .endproc

            .proc scl_low
                lda #SCL
                tsb V_DDRA
                rts
            .endproc

            .proc scl_high
                lda #SCL
                trb V_DDRA
            .loop:
                lda V_PRA     ; Wait for clock to go high
                and #SCL
                beq loop
                rts
            .endproc

            .proc send_bit
                bcs high
                jsr sda_low
                bra done
            .high:
                jsr sda_high
            .done:
                jsr scl_high
                jsr scl_low
                rts
            .endproc

            .proc i2c_init
                lda #SDA | SCL
                trb V_PRA
                jsr sda_high
                jsr scl_high
                rts
            .endproc

            .proc rec_bit
                jsr sda_high		; Release SDA so that device can drive it
                jsr scl_high
                lda V_PRA
                lsr             ; bit -> C
                jsr scl_low
                rts
            .endproc

            .proc i2c_brief_delay
                nop
                nop
                nop
                nop
                rts
            .endproc",
                emulator);

        Assert.AreEqual((uint)0xcdab, emulator.Smc.Data);
        Assert.AreEqual((uint)2, emulator.Smc.DataCount);
    }

    [TestMethod]
    public async Task Data_3Bytes()
    {
        var emulator = new Emulator();

        await X16TestHelper.Emulate(@"
            .machine CommanderX16R42
            .org $810
            ; DDRA is data direction.
            ; PRA is the data

            .const SDA = 1
            .const SCL = 2

                lda #$cd
                ldx #$42
                ldy #$ab

                jsr write_first_byte

                lda #$ef
                jsr i2c_write_next_byte

                stp

            ;---------------------------------------------------------------
            ; i2c_write_first_byte
            ; 
            ; Function: Writes one byte over I2C without stopping the
            ;           transmission. Subsequent bytes may be written by
            ;           i2c_write_next_byte. When done, call function
            ;           i2c_write_stop to close the I2C transmission.
            ;
            ; Pass:      a    value
            ;            x    7-bit device address
            ;            y    offset
            ;
            ; Return:    c    1 on error (NAK)
            ;---------------------------------------------------------------
            .proc write_first_byte
                pha                ; value
                jsr i2c_init
                jsr i2c_start
                txa                ; device
                asl
                phy
                jsr i2c_write
                ply
                bcs error
                tya                ; offset
                phy
                jsr i2c_write
                ply
                pla                ; value
                jsr i2c_write
                clc
                rts

            .error:
                pla                ; value
                sec
                rts
            .endproc

            ;---------------------------------------------------------------
            ; i2c_write_next_byte
            ;
            ; Function:	After the first byte has been written by 
            ;			i2c_write_first_byte, this function may be used to
            ;			write one or more subsequent bytes without 
            ;			restarting the I2C transmission
            ;
            ; Pass:		a    value
            ;
            ; Return:	Nothing
            ;---------------------------------------------------------------
            .proc i2c_write_next_byte
                jmp i2c_write
            .endproc

            ;---------------------------------------------------------------
            ; i2c_write
            ;
            ; Function: Write a single byte over I2C
            ;
            ; Pass:      a    byte to write
            ;
            ; Return:    c    0 if ACK, 1 if NAK
            ;
            ; I2C Exit:  SDA: Z
            ;            SCL: 0
            ;---------------------------------------------------------------
            .proc i2c_write
                ldx #8
            .i2c_write_loop:
                rol
                tay
                jsr send_bit
                tya
                dex
                bne i2c_write_loop
                jsr rec_bit     ; C = 0: success
                rts
            .endproc

            ;---------------------------------------------------------------
            ; i2c_write_stop
            ;
            ; Function:	Stops I2C transmission that has been initialized
            ;			with i2c_write_first_byte
            ;
            ; Pass:		Nothing
            ;
            ; Return:	Nothing
            ;---------------------------------------------------------------
            .proc i2c_write_stop
                jsr i2c_stop
            .endproc


            .proc i2c_start
                jsr sda_low
                jsr i2c_brief_delay
                jsr scl_low
                rts
            .endproc

            .proc i2c_stop
                jsr sda_low
                jsr i2c_brief_delay
                jsr scl_high
                jsr i2c_brief_delay
                jsr sda_high
                jsr i2c_brief_delay
                rts
            .endproc

            .proc sda_low
                lda #SDA
                tsb V_DDRA
                rts
            .endproc

            .proc sda_high
                lda #SDA
                trb V_DDRA
                rts
            .endproc

            .proc scl_low
                lda #SCL
                tsb V_DDRA
                rts
            .endproc

            .proc scl_high
                lda #SCL
                trb V_DDRA
            .loop:
                lda V_PRA     ; Wait for clock to go high
                and #SCL
                beq loop
                rts
            .endproc

            .proc send_bit
                bcs high
                jsr sda_low
                bra done
            .high:
                jsr sda_high
            .done:
                jsr scl_high
                jsr scl_low
                rts
            .endproc

            .proc i2c_init
                lda #SDA | SCL
                trb V_PRA
                jsr sda_high
                jsr scl_high
                rts
            .endproc

            .proc rec_bit
                jsr sda_high		; Release SDA so that device can drive it
                jsr scl_high
                lda V_PRA
                lsr             ; bit -> C
                jsr scl_low
                rts
            .endproc

            .proc i2c_brief_delay
                nop
                nop
                nop
                nop
                rts
            .endproc",
                emulator);

        Assert.AreEqual((uint)0xefcdab, emulator.Smc.Data);
        Assert.AreEqual((uint)3, emulator.Smc.DataCount);
    }

    [TestMethod]
    public async Task Data_4Bytes()
    {
        var emulator = new Emulator();

        await X16TestHelper.Emulate(@"
            .machine CommanderX16R42
            .org $810
            ; DDRA is data direction.
            ; PRA is the data

            .const SDA = 1
            .const SCL = 2

                lda #$cd
                ldx #$42
                ldy #$ab

                jsr write_first_byte

                lda #$ef
                jsr i2c_write_next_byte

                lda #$34
                jsr i2c_write_next_byte

                stp

            ;---------------------------------------------------------------
            ; i2c_write_first_byte
            ; 
            ; Function: Writes one byte over I2C without stopping the
            ;           transmission. Subsequent bytes may be written by
            ;           i2c_write_next_byte. When done, call function
            ;           i2c_write_stop to close the I2C transmission.
            ;
            ; Pass:      a    value
            ;            x    7-bit device address
            ;            y    offset
            ;
            ; Return:    c    1 on error (NAK)
            ;---------------------------------------------------------------
            .proc write_first_byte
                pha                ; value
                jsr i2c_init
                jsr i2c_start
                txa                ; device
                asl
                phy
                jsr i2c_write
                ply
                bcs error
                tya                ; offset
                phy
                jsr i2c_write
                ply
                pla                ; value
                jsr i2c_write
                clc
                rts

            .error:
                pla                ; value
                sec
                rts
            .endproc

            ;---------------------------------------------------------------
            ; i2c_write_next_byte
            ;
            ; Function:	After the first byte has been written by 
            ;			i2c_write_first_byte, this function may be used to
            ;			write one or more subsequent bytes without 
            ;			restarting the I2C transmission
            ;
            ; Pass:		a    value
            ;
            ; Return:	Nothing
            ;---------------------------------------------------------------
            .proc i2c_write_next_byte
                jmp i2c_write
            .endproc

            ;---------------------------------------------------------------
            ; i2c_write
            ;
            ; Function: Write a single byte over I2C
            ;
            ; Pass:      a    byte to write
            ;
            ; Return:    c    0 if ACK, 1 if NAK
            ;
            ; I2C Exit:  SDA: Z
            ;            SCL: 0
            ;---------------------------------------------------------------
            .proc i2c_write
                ldx #8
            .i2c_write_loop:
                rol
                tay
                jsr send_bit
                tya
                dex
                bne i2c_write_loop
                jsr rec_bit     ; C = 0: success
                rts
            .endproc

            ;---------------------------------------------------------------
            ; i2c_write_stop
            ;
            ; Function:	Stops I2C transmission that has been initialized
            ;			with i2c_write_first_byte
            ;
            ; Pass:		Nothing
            ;
            ; Return:	Nothing
            ;---------------------------------------------------------------
            .proc i2c_write_stop
                jsr i2c_stop
            .endproc


            .proc i2c_start
                jsr sda_low
                jsr i2c_brief_delay
                jsr scl_low
                rts
            .endproc

            .proc i2c_stop
                jsr sda_low
                jsr i2c_brief_delay
                jsr scl_high
                jsr i2c_brief_delay
                jsr sda_high
                jsr i2c_brief_delay
                rts
            .endproc

            .proc sda_low
                lda #SDA
                tsb V_DDRA
                rts
            .endproc

            .proc sda_high
                lda #SDA
                trb V_DDRA
                rts
            .endproc

            .proc scl_low
                lda #SCL
                tsb V_DDRA
                rts
            .endproc

            .proc scl_high
                lda #SCL
                trb V_DDRA
            .loop:
                lda V_PRA     ; Wait for clock to go high
                and #SCL
                beq loop
                rts
            .endproc

            .proc send_bit
                bcs high
                jsr sda_low
                bra done
            .high:
                jsr sda_high
            .done:
                jsr scl_high
                jsr scl_low
                rts
            .endproc

            .proc i2c_init
                lda #SDA | SCL
                trb V_PRA
                jsr sda_high
                jsr scl_high
                rts
            .endproc

            .proc rec_bit
                jsr sda_high		; Release SDA so that device can drive it
                jsr scl_high
                lda V_PRA
                lsr             ; bit -> C
                jsr scl_low
                rts
            .endproc

            .proc i2c_brief_delay
                nop
                nop
                nop
                nop
                rts
            .endproc",
                emulator);

        Assert.AreEqual((uint)0xefcdab, emulator.Smc.Data);
        Assert.AreEqual((uint)3, emulator.Smc.DataCount);
    }
}