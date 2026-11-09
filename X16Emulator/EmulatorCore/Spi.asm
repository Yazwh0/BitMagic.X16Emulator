;    Copyright (C) 2022 BJ

;    This program is free software: you can redistribute it and/or modify
;    it under the terms of the GNU General Public License as published by
;    the Free Software Foundation, either version 3 of the License, or
;    (at your option) any later version.

;    This program is distributed in the hope that it will be useful,
;    but WITHOUT ANY WARRANTY; without even the implied warranty of
;    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
;    GNU General Public License for more details.

;    You should have received a copy of the GNU General Public License
;    along with this program.  If not, see https://www.gnu.org/licenses/.
.code

SPI_PARAMETER_ERROR equ 01000000b
SPI_ADDRESS_ERROR	equ 00100000b
SPI_ERASE_SEQ_ERROR equ 00010000b
SPI_CRC_ERROR		equ 00001000b
SPI_ILLEGAL_COMMAND equ 00000100b
SPI_ERASE_RESET		equ 00000010b
SPI_IDLE_STATE		equ 00000001b

SPI_SELECT		equ 001b
SPI_SLOWCLOCK	equ 010b
SPI_AUTOTX		equ 100b

SPI_DEFAULT_RESPONSE equ 0ffh

spi_log_fromsdcard macro
	;mov eax, dword ptr [rdx].state.spi_position
	;mov rbx, qword ptr [rdx].state.spi_history_ptr
	;mov byte ptr [rbx + rax+1], 1		; input (from sdcard)
	;mov byte ptr [rbx + rax], r13b
	;add eax, 2
	;and eax, 2048-1
	;mov dword ptr [rdx].state.spi_position, eax
endm

vera_update_spi_data proc
	movzx r13, byte ptr [rsi+SPI_DATA]

	; Logging
	;mov eax, dword ptr [rdx].state.spi_position
	;mov rbx, qword ptr [rdx].state.spi_history_ptr
	;mov byte ptr [rbx + rax+1], 0		; output (to sdcard)
	;mov byte ptr [rbx + rax], r13b
	;add eax, 2
	;and eax, 2048-1
	;mov dword ptr [rdx].state.spi_position, eax

	; no matter what, if there is no sdcard then we dont reply
	mov rax, qword ptr [rdx].state.sdcard_ptr
	test rax, rax
	jz dont_change_message

	; if select is 0 then ignore completely.
	mov eax, dword ptr [rdx].state.spi_chipselect
	test eax, eax
	jz dont_change_message

	; while blocks are being read (CMD17 or CMD18), every byte clocks out the next byte whatever the host sends, as SPI
	; is full duplex. a command can arrive at the same time, see spi_command_mid_reply. only for a read: a short reply
	; the host doesn't read all of, eg CMD8's, doesn't stop the next command.
	mov eax, dword ptr [rdx].state.spi_sendlength
	test eax, eax
	jz not_sending

	mov eax, dword ptr [rdx].state.spi_previouscommand
	cmp eax, 17
	je reading
	cmp eax, 18
	jne not_sending
reading:

	mov ebx, dword ptr [rdx].state.spi_receivecount
	test ebx, ebx
	jnz collect_mid_reply

	mov al, r13b						; only a command's first byte (01xxxxxx) starts one
	and al, 11000000b
	cmp al, 01000000b
	jne spi_send_data

collect_mid_reply:
	mov rax, qword ptr [rdx].state.spi_inbound_buffer_ptr
	mov byte ptr [rax + rbx], r13b
	inc ebx
	mov dword ptr [rdx].state.spi_receivecount, ebx
	cmp ebx, 6
	je spi_command_mid_reply
	jmp spi_send_data

not_sending:
	; ignore all 0xff's if we have an empty receive buffer

	; do we have data?
	mov ebx, dword ptr [rdx].state.spi_receivecount
	test rbx, rbx
	jnz receiving_command

	; if no inbounddata, if 0xff then see if there is outbound data
	cmp r13b, 0ffh
	je spi_send_data

receiving_command:
	; shift and or on the new data, then check
	mov rax, qword ptr [rdx].state.spi_inbound_buffer_ptr
	mov byte ptr [rax + rbx], r13b

	inc ebx
	mov dword ptr [rdx].state.spi_receivecount, ebx

	mov r13d, dword ptr [rdx].state.spi_writeblock
	test r13d, r13d
	jnz writing_block

	; check command
	cmp ebx, 6
	je spi_handle_command

default_message:
	mov r13b, SPI_DEFAULT_RESPONSE
	mov byte ptr [rsi+SPI_DATA], r13b
	mov dword ptr [rdx].state.spi_previousvalue, r13d

	spi_log_fromsdcard

	ret

dont_change_message:
	mov r13d, dword ptr [rdx].state.spi_previousvalue
	mov byte ptr [rsi+SPI_DATA], r13b
	
	spi_log_fromsdcard

	ret

writing_block:
	cmp ebx, 515
	je spi_write_complete

	mov r13b, SPI_DEFAULT_RESPONSE
	mov byte ptr [rsi+SPI_DATA], r13b
	mov dword ptr [rdx].state.spi_previousvalue, r13d

	spi_log_fromsdcard

	ret
vera_update_spi_data endp

vera_afterread_spidata proc

	mov eax, dword ptr [rdx].state.spi_autotx
	test eax, eax
	jz not_autotx

	; check if 7 cpu ticks have elapsed since last byte sent
	cmp qword ptr [rdx].state.spi_nextcpuread, r14
	jg return_previous

	mov ebx, dword ptr [rdx].state.spi_sendlength
	test rbx, rbx
	jz spi_do_nothing

	; if autotx is on, then place the next byte on the dataport
	; put data into reply
	mov ebx, dword ptr [rdx].state.spi_sendcount
	mov rax, qword ptr [rdx].state.spi_outbound_buffer_ptr
	movzx r13, byte ptr [rax + rbx]
	mov byte ptr [rsi+SPI_DATA], r13b
	mov dword ptr [rdx].state.spi_previousvalue, r13d

	inc rbx

	mov r13d, dword ptr [rdx].state.spi_sendlength


	xor rax, rax
	cmp r13d, ebx									; are we done?

	cmove rbx, rax									; if done clear sendlength and sendcount
	cmove r13, rax									

	mov rax, r14									; store next cpu that a read can occur
	add rax, 7
	mov qword ptr [rdx].state.spi_nextcpuread, rax

	mov dword ptr [rdx].state.spi_sendcount, ebx
	mov dword ptr [rdx].state.spi_sendlength, r13d

	test r13d, r13d									; finished a block of a CMD18, so on to the next
	jnz not_autotx
	call spi_reply_finished

not_autotx:
	ret

return_previous:
	mov r13d, dword ptr [rdx].state.spi_previousvalue

	ret

vera_afterread_spidata endp

spi_do_nothing proc
	mov r13b, SPI_DEFAULT_RESPONSE
	mov byte ptr [rsi+SPI_DATA], r13b
	mov dword ptr [rdx].state.spi_previousvalue, r13d

	spi_log_fromsdcard

	ret
spi_do_nothing endp

spi_not_known proc
	mov r13b, SPI_DEFAULT_RESPONSE
	mov byte ptr [rsi+SPI_DATA], r13b
	mov dword ptr [rdx].state.spi_previousvalue, r13d

	spi_log_fromsdcard

	ret
spi_not_known endp

; sends data from SDCARD to X16.
spi_send_data proc
	mov ebx, dword ptr [rdx].state.spi_sendlength
	test rbx, rbx
	jz spi_do_nothing

	; put data into reply
	mov ebx, dword ptr [rdx].state.spi_sendcount
	mov rax, qword ptr [rdx].state.spi_outbound_buffer_ptr
	movzx r13, byte ptr [rax + rbx]
	mov byte ptr [rsi+SPI_DATA], r13b
	mov dword ptr [rdx].state.spi_previousvalue, r13d

	inc rbx

	mov r13d, dword ptr [rdx].state.spi_sendlength


	xor rax, rax
	cmp r13d, ebx									; are we done?

	cmove rbx, rax									; if done clear sendlength and sendcount
	cmove r13, rax									

	mov rax, r14									; store next cpu that a read can occur
	add rax, 7
	mov qword ptr [rdx].state.spi_nextcpuread, rax


	mov dword ptr [rdx].state.spi_sendcount, ebx
	mov dword ptr [rdx].state.spi_sendlength, r13d

	test r13d, r13d									; finished a block of a CMD18, so on to the next
	jnz not_finished
	call spi_reply_finished
not_finished:

	movzx r13, byte ptr [rsi+SPI_DATA] ; for logging

	spi_log_fromsdcard

	ret
spi_send_data endp

; A reply has just been sent. If it was a block of a multiple block read (CMD18), the next block follows: its $FE
; token, the data and the CRC. Past the end of the card an error token ($08, out of range) is sent instead, and the
; read ends. (Hardware sends a few $FF before each token, checked on a real X16, issue #216. The host waits for the
; token, so they're left out.)
spi_reply_finished proc
	mov eax, dword ptr [rdx].state.spi_previouscommand
	cmp eax, 18
	jne done

	mov ebx, dword ptr [rdx].state.spi_sectorread
	inc ebx
	mov dword ptr [rdx].state.spi_sectorread, ebx

	mov rax, qword ptr [rdx].state.spi_outbound_buffer_ptr
	mov dword ptr [rdx].state.spi_sendcount, 0

	mov r13d, dword ptr [rdx].state.spi_sdcardsize	; number of blocks
	cmp rbx, r13
	jae past_end

	mov byte ptr [rax], 0feh						; token
	mov byte ptr [rax + 1 + 512], 0					; crc bytes
	mov byte ptr [rax + 1 + 512 + 1], 0
	inc rax
	call spi_copy_block
	mov dword ptr [rdx].state.spi_sendlength, 1 + 512 + 2
	ret

past_end:
	mov byte ptr [rax], 08h							; error token, out of range
	mov dword ptr [rdx].state.spi_sendlength, 1
	mov dword ptr [rdx].state.spi_previouscommand, 0	; the read is over
done:
	ret
spi_reply_finished endp

; A whole command has arrived while blocks were being read. On hardware (SD spec 7.2.8, checked on a real X16, issues
; #195 and #216) the card keeps sending the block while the command arrives, then drops the read and sends a stuff
; byte and an R1: CMD12 (stop transmission) is accepted, R1 $00 (the spec allows busy after it, the card tested gave
; none). Anything else is rejected, R1 $04 (illegal command), and isn't carried out. The stuff byte's value isn't
; defined, $FF is used.
spi_command_mid_reply proc
	mov dword ptr [rdx].state.spi_receivecount, 0

	call spi_send_data					; this byte still gets the reply's next byte

	; if that was the reply's last byte, the command came just as it finished, so it's a normal command
	mov eax, dword ptr [rdx].state.spi_sendlength
	test eax, eax
	jz after_reply

	mov rax, qword ptr [rdx].state.spi_inbound_buffer_ptr
	movzx ebx, byte ptr [rax]
	and ebx, 00111111b

	mov rax, qword ptr [rdx].state.spi_outbound_buffer_ptr
	mov byte ptr [rax], 0ffh			; stuff byte
	mov dword ptr [rdx].state.spi_sendcount, 0
	mov dword ptr [rdx].state.spi_sendlength, 2

	cmp ebx, 12
	jne rejected

	mov byte ptr [rax + 1], 0			; R1, accepted
	mov dword ptr [rdx].state.spi_previouscommand, ebx	; the read is over, so this reply isn't a read
	ret

rejected:
	mov byte ptr [rax + 1], SPI_ILLEGAL_COMMAND	; R1
	mov dword ptr [rdx].state.spi_previouscommand, 0	; the read is over, and the command wasn't carried out
	ret

after_reply:
	; run it, but keep the reply byte just sent rather than the $FF spi_handle_command puts out
	movzx r13, byte ptr [rsi+SPI_DATA]
	push r13
	mov rax, qword ptr [rdx].state.spi_inbound_buffer_ptr
	call spi_handle_command
	pop r13
	mov byte ptr [rsi+SPI_DATA], r13b
	mov dword ptr [rdx].state.spi_previousvalue, r13d
	ret
spi_command_mid_reply endp

spi_handle_command proc
	mov r13b, SPI_DEFAULT_RESPONSE
	mov byte ptr [rsi+SPI_DATA], r13b
	mov dword ptr [rdx].state.spi_previousvalue, r13d

	; clear receive count, so it can start again
	mov dword ptr [rdx].state.spi_receivecount, 0

	movzx rbx, byte ptr [rax] ; get command
	and rbx, 00111111b
	mov dword ptr [rdx].state.spi_previouscommand, ebx
	lea rdi, spi_command_table
	add rdi, [rdi + rbx * 8]
	jmp rdi
spi_handle_command endp

spi_go_idle proc
	; place reset state into transmit buffer
	mov rax, qword ptr [rdx].state.spi_outbound_buffer_ptr
	mov byte ptr [rax], SPI_IDLE_STATE
	xor rax, rax
	mov dword ptr [rdx].state.spi_sendcount, eax
	inc eax
	mov dword ptr [rdx].state.spi_sendlength, eax
	mov dword ptr [rdx].state.spi_idle, eax

	spi_log_fromsdcard

	ret
spi_go_idle endp

spi_cmd8 proc
	; place reset state into transmit buffer
	mov rax, qword ptr [rdx].state.spi_outbound_buffer_ptr
	; magic values, for voltage etc, taken from the official emulator
	mov dword ptr [rax], 01000001h
	mov dword ptr [rax + 4], 0aah
	mov dword ptr [rdx].state.spi_sendcount, 0
	mov dword ptr [rdx].state.spi_sendlength, 5

	spi_log_fromsdcard

	ret
spi_cmd8 endp

spi_cmd55 proc	
	mov dword ptr [rdx].state.spi_commandnext, 1	; application specific command

	mov rax, qword ptr [rdx].state.spi_outbound_buffer_ptr
	mov ebx, dword ptr [rdx].state.spi_idle
	mov byte ptr [rax], bl
	mov dword ptr [rdx].state.spi_sendcount, 0
	mov dword ptr [rdx].state.spi_sendlength, 1

	spi_log_fromsdcard
	ret
spi_cmd55 endp

spi_acmd41 proc
	mov dword ptr [rdx].state.spi_commandnext, 1	; application specific command

	mov rax, qword ptr [rdx].state.spi_outbound_buffer_ptr
	mov dword ptr [rdx].state.spi_idle, 0			; no longer idle
	mov byte ptr [rax], 0
	mov dword ptr [rdx].state.spi_sendcount, 0
	mov dword ptr [rdx].state.spi_sendlength, 1
	mov dword ptr [rdx].state.spi_initialised, 1	; now initilised

	spi_log_fromsdcard

	ret
spi_acmd41 endp

spi_read_single_block proc
	; get requested block number
	mov rax, qword ptr [rdx].state.spi_inbound_buffer_ptr ; need to get the parameter

	xor rbx, rbx
	mov bl, byte ptr [rax + 1]
	shl rbx, 8
	mov bl, byte ptr [rax + 2]
	shl rbx, 8
	mov bl, byte ptr [rax + 3]
	shl rbx, 8
	mov bl, byte ptr [rax + 4]

	mov dword ptr [rdx].state.spi_sectorread, ebx

	mov r13d, dword ptr [rdx].state.spi_sdcardsize	; number of blocks, so the last is size - 1
	cmp rbx, r13
	jae spi_do_nothing ; not sure what to do if we exceed the disk size

	; fill outbound buffer: R1, token, the block, crc
	mov rax, qword ptr [rdx].state.spi_outbound_buffer_ptr
	mov byte ptr [rax], 0
	mov byte ptr [rax+1], 0feh
	mov byte ptr [rax+512+2], 0			; crc bytes
	mov byte ptr [rax+512+2+1], 0
	add rax, 2
	call spi_copy_block

	mov dword ptr [rdx].state.spi_sendcount, 0
	mov dword ptr [rdx].state.spi_sendlength, 512 + 2 + 2

	spi_log_fromsdcard

	ret
spi_read_single_block endp

; copies block rbx of the sd card (already checked to be on it) to rax
spi_copy_block proc
	shl rbx, 9	; * 512 to get offset of the sd card
	mov rdi, qword ptr [rdx].state.sdcard_ptr

	; sd card is alligned, buffer isn't
	vmovdqa ymm0, ymmword ptr [rdi + rbx + 00h]
	vmovdqu ymmword ptr [rax + 00h], ymm0
	vmovdqa ymm1, ymmword ptr [rdi + rbx + 20h]
	vmovdqu ymmword ptr [rax + 20h], ymm1
	vmovdqa ymm2, ymmword ptr [rdi + rbx + 40h]
	vmovdqu ymmword ptr [rax + 40h], ymm2
	vmovdqa ymm3, ymmword ptr [rdi + rbx + 60h]
	vmovdqu ymmword ptr [rax + 60h], ymm3

	vmovdqa ymm4, ymmword ptr [rdi + rbx + 080h]
	vmovdqu ymmword ptr [rax + 080h], ymm4
	vmovdqa ymm5, ymmword ptr [rdi + rbx + 0a0h]
	vmovdqu ymmword ptr [rax + 0a0h], ymm5
	vmovdqa ymm6, ymmword ptr [rdi + rbx + 0c0h]
	vmovdqu ymmword ptr [rax + 0c0h], ymm6
	vmovdqa ymm7, ymmword ptr [rdi + rbx + 0e0h]
	vmovdqu ymmword ptr [rax + 0e0h], ymm7

	vmovdqa ymm8, ymmword ptr [rdi + rbx + 100h]
	vmovdqu ymmword ptr [rax + 100h], ymm8
	vmovdqa ymm9, ymmword ptr [rdi + rbx + 120h]
	vmovdqu ymmword ptr [rax + 120h], ymm9
	vmovdqa ymm10, ymmword ptr [rdi + rbx + 140h]
	vmovdqu ymmword ptr [rax + 140h], ymm10
	vmovdqa ymm11, ymmword ptr [rdi + rbx + 160h]
	vmovdqu ymmword ptr [rax + 160h], ymm11

	vmovdqa ymm12, ymmword ptr [rdi + rbx + 180h]
	vmovdqu ymmword ptr [rax + 180h], ymm12
	vmovdqa ymm13, ymmword ptr [rdi + rbx + 1a0h]
	vmovdqu ymmword ptr [rax + 1a0h], ymm13
	vmovdqa ymm14, ymmword ptr [rdi + rbx + 1c0h]
	vmovdqu ymmword ptr [rax + 1c0h], ymm14
	vmovdqa ymm15, ymmword ptr [rdi + rbx + 1e0h]
	vmovdqu ymmword ptr [rax + 1e0h], ymm15

	ret
spi_copy_block endp

; CMD12 with no read going on: accepted, R1 $00 (checked on a real X16, issue #216). During a read it's handled by
; spi_command_mid_reply.
spi_stop_transmission proc
	mov rax, qword ptr [rdx].state.spi_outbound_buffer_ptr
	mov byte ptr [rax], 0
	mov dword ptr [rdx].state.spi_sendcount, 0
	mov dword ptr [rdx].state.spi_sendlength, 1

	spi_log_fromsdcard

	ret
spi_stop_transmission endp

spi_write_single_block proc
	; get requested block number
	mov rax, qword ptr [rdx].state.spi_inbound_buffer_ptr ; need to get the parameter

	xor rbx, rbx
	mov bl, byte ptr [rax + 1]
	shl rbx, 8
	mov bl, byte ptr [rax + 2]
	shl rbx, 8
	mov bl, byte ptr [rax + 3]
	shl rbx, 8
	mov bl, byte ptr [rax + 4]

	mov r13d, dword ptr [rdx].state.spi_sdcardsize	; number of blocks, so the last is size - 1
	cmp rbx, r13
	jae spi_do_nothing ; not sure what to do if we exceed the disk size

	; ebx now contains the block number. store it + 1, so it's non zero while we're receiving. the byte offset can
	; be more than 32 bits, so it's worked out when the data has arrived.
	inc ebx

	mov dword ptr [rdx].state.spi_writeblock, ebx

	xor r13, r13
	mov byte ptr [rax], r13b
	mov dword ptr [rdx].state.spi_sendcount, r13d
	mov dword ptr [rdx].state.spi_sendlength, 1

	spi_log_fromsdcard

	ret
spi_write_single_block endp

spi_write_complete proc
	; check start block byte
	cmp byte ptr [rax], 0feh
	jne bad_data

	mov ebx, dword ptr [rdx].state.spi_previouscommand
	cmp ebx, 24
	jne bad_data

	; rax is the inbound data buffer
	mov rdi, qword ptr [rdx].state.sdcard_ptr
	mov ebx, dword ptr [rdx].state.spi_writeblock
	dec ebx		; block number, stored + 1
	shl rbx, 9	; * 512 to get offset of the sd card (mov ebx cleared the top of rbx)

	; copying from buffer to sdcard
	; sd card is aligned, buffer isn't
	vmovdqu ymm0, ymmword ptr [rax + 1 + 00h]
	vmovdqa ymmword ptr [rdi + rbx + 00h], ymm0
	vmovdqu ymm1, ymmword ptr [rax + 1 + 20h]
	vmovdqa ymmword ptr [rdi + rbx + 20h], ymm1
	vmovdqu ymm2, ymmword ptr [rax + 1 + 40h]
	vmovdqa ymmword ptr [rdi + rbx + 40h], ymm2
	vmovdqu ymm3, ymmword ptr [rax + 1 + 60h]
	vmovdqa ymmword ptr [rdi + rbx + 60h], ymm3

	vmovdqu ymm4, ymmword ptr [rax + 1 + 080h]
	vmovdqa ymmword ptr [rdi + rbx + 080h], ymm4
	vmovdqu ymm5, ymmword ptr [rax + 1 + 0a0h]
	vmovdqa ymmword ptr [rdi + rbx + 0a0h], ymm5
	vmovdqu ymm6, ymmword ptr [rax + 1 + 0c0h]
	vmovdqa ymmword ptr [rdi + rbx + 0c0h], ymm6
	vmovdqu ymm7, ymmword ptr [rax + 1 + 0e0h]
	vmovdqa ymmword ptr [rdi + rbx + 0e0h], ymm7

	vmovdqu ymm8, ymmword ptr [rax + 1 + 100h]
	vmovdqa ymmword ptr [rdi + rbx + 100h], ymm8
	vmovdqu ymm9, ymmword ptr [rax + 1 + 120h]
	vmovdqa ymmword ptr [rdi + rbx + 120h], ymm9
	vmovdqu ymm10, ymmword ptr [rax + 1 + 140h]
	vmovdqa ymmword ptr [rdi + rbx + 140h], ymm10
	vmovdqu ymm11, ymmword ptr [rax + 1 + 160h]
	vmovdqa ymmword ptr [rdi + rbx + 160h], ymm11

	vmovdqu ymm12, ymmword ptr [rax + 1 + 180h]
	vmovdqa ymmword ptr [rdi + rbx + 180h], ymm12
	vmovdqu ymm13, ymmword ptr [rax + 1 + 1a0h]
	vmovdqa ymmword ptr [rdi + rbx + 1a0h], ymm13
	vmovdqu ymm14, ymmword ptr [rax + 1 + 1c0h]
	vmovdqa ymmword ptr [rdi + rbx + 1c0h], ymm14
	vmovdqu ymm15, ymmword ptr [rax + 1 + 1e0h]
	vmovdqa ymmword ptr [rdi + rbx + 1e0h], ymm15

	xor rbx, rbx
	mov dword ptr [rdx].state.spi_receivecount, ebx
	mov dword ptr [rdx].state.spi_writeblock, ebx

bad_data:
	mov r13b, SPI_DEFAULT_RESPONSE
	mov byte ptr [rsi+SPI_DATA], r13b
	mov dword ptr [rdx].state.spi_previousvalue, r13d

	spi_log_fromsdcard

	ret	
spi_write_complete endp

spi_read_send_status proc
	spi_log_fromsdcard

	mov dword ptr [rdx].state.spi_sendcount, 0
	mov dword ptr [rdx].state.spi_sendlength, 2

	mov eax, dword ptr [rdx].state.spi_initialised
	test eax, eax
	jz not_initialised

	xor rbx, rbx
	mov rax, qword ptr [rdx].state.spi_outbound_buffer_ptr
	mov byte ptr [rax], bl
	mov byte ptr [rax+1], bl

	ret
not_initialised:
	mov rax, qword ptr [rdx].state.spi_outbound_buffer_ptr
	mov byte ptr [rax], 1fh
	mov byte ptr [rax+1], 0ffh

	ret
spi_read_send_status endp

spi_read_ocr proc
	spi_log_fromsdcard

	mov dword ptr [rdx].state.spi_sendcount, 0
	mov dword ptr [rdx].state.spi_sendlength, 4

	mov rax, qword ptr [rdx].state.spi_outbound_buffer_ptr
	mov byte ptr [rax], 0c0h
	mov byte ptr [rax+1], 0ffh
	mov byte ptr [rax+2], 080h
	mov byte ptr [rax+3], 000h

	ret
spi_read_ocr endp

spi_read_csd proc
	spi_log_fromsdcard

	mov dword ptr [rdx].state.spi_sendcount, 0
	mov dword ptr [rdx].state.spi_sendlength, 16

	mov rax, qword ptr [rdx].state.spi_outbound_buffer_ptr
	mov rbx, qword ptr [rdx].state.spi_csd_register0
	mov qword ptr [rax], rbx
	mov rbx, qword ptr [rdx].state.spi_csd_register1
	mov qword ptr [rax+8], rbx

	ret
spi_read_csd endp

align 8
spi_command_table:
	qword spi_go_idle	- spi_command_table ;  0
	qword spi_not_known - spi_command_table ;  1
	qword spi_not_known - spi_command_table ;  2
	qword spi_not_known - spi_command_table ;  3
	qword spi_not_known - spi_command_table ;  4
	qword spi_not_known - spi_command_table ;  5
	qword spi_not_known - spi_command_table ;  6
	qword spi_not_known - spi_command_table ;  7
	qword spi_cmd8		- spi_command_table ;  8
	qword spi_read_csd	- spi_command_table ;  9
	qword spi_not_known - spi_command_table ;  10
	qword spi_not_known - spi_command_table ;  11
	qword spi_stop_transmission - spi_command_table ;  12
	qword spi_read_send_status - spi_command_table ;  13
	qword spi_not_known - spi_command_table ;  14
	qword spi_not_known - spi_command_table ;  15
	qword spi_not_known - spi_command_table ;  16
	qword spi_read_single_block - spi_command_table ;  17
	qword spi_read_single_block - spi_command_table ;  18, the same start, spi_reply_finished sends the next blocks
	qword spi_not_known - spi_command_table ;  19
	qword spi_not_known - spi_command_table ;  20
	qword spi_not_known - spi_command_table ;  21
	qword spi_not_known - spi_command_table ;  22
	qword spi_not_known - spi_command_table ;  23
	qword spi_write_single_block - spi_command_table ;  24
	qword spi_not_known - spi_command_table ;  25
	qword spi_not_known - spi_command_table ;  26
	qword spi_not_known - spi_command_table ;  27
	qword spi_not_known - spi_command_table ;  28
	qword spi_not_known - spi_command_table ;  29
	qword spi_not_known - spi_command_table ;  30
	qword spi_not_known - spi_command_table ;  31
	qword spi_not_known - spi_command_table ;  32
	qword spi_not_known - spi_command_table ;  33
	qword spi_not_known - spi_command_table ;  34
	qword spi_not_known - spi_command_table ;  35
	qword spi_not_known - spi_command_table ;  36
	qword spi_not_known - spi_command_table ;  37
	qword spi_not_known - spi_command_table ;  38
	qword spi_not_known - spi_command_table ;  39
	qword spi_not_known - spi_command_table ;  40
	qword spi_acmd41	- spi_command_table ;  41
	qword spi_not_known - spi_command_table ;  42
	qword spi_not_known - spi_command_table ;  43
	qword spi_not_known - spi_command_table ;  44
	qword spi_not_known - spi_command_table ;  45
	qword spi_not_known - spi_command_table ;  46
	qword spi_not_known - spi_command_table ;  47
	qword spi_not_known - spi_command_table ;  48
	qword spi_not_known - spi_command_table ;  49
	qword spi_not_known - spi_command_table ;  50
	qword spi_not_known - spi_command_table ;  51
	qword spi_not_known - spi_command_table ;  52
	qword spi_not_known - spi_command_table ;  53
	qword spi_not_known - spi_command_table ;  54
	qword spi_cmd55		- spi_command_table ;  55
	qword spi_not_known - spi_command_table ;  56
	qword spi_not_known - spi_command_table ;  57
	qword spi_read_ocr  - spi_command_table ;  58
	qword spi_not_known - spi_command_table ;  59
	qword spi_not_known - spi_command_table ;  60
	qword spi_not_known - spi_command_table ;  61
	qword spi_not_known - spi_command_table ;  62
	qword spi_not_known - spi_command_table ;  63
	qword spi_not_known - spi_command_table ;  64


vera_update_spi_ctrl proc
	movzx r13, byte ptr [rsi+SPI_CTRL]
	and r13b, SPI_SELECT + SPI_SLOWCLOCK + SPI_AUTOTX		; can only set slow clock, select or autotx.
	mov byte ptr [rsi+SPI_CTRL], r13b

	mov eax, r13d
	and eax, SPI_AUTOTX
	shr eax, 2
	mov dword ptr [rdx].state.spi_autotx, eax

	mov ebx, dword ptr [rdx].state.spi_chipselect
	and r13d, 1
	mov dword ptr [rdx].state.spi_chipselect, r13d			; chip select is always set
	xor ebx, r13d
	jz no_change											; we only reset receive count on a change

	mov ebx, dword ptr [rdx].state.spi_receivecount
	xor rax, rax

	bt r13, 0
	cmovc ebx, eax											; clear the receive count if select is 1
	mov dword ptr [rdx].state.spi_receivecount, ebx			
	
no_change:
	; Log ctrl -----------------
	mov eax, dword ptr [rdx].state.spi_position
	mov rbx, qword ptr [rdx].state.spi_history_ptr
	mov byte ptr [rbx + rax+1], 2		; ctrl
	mov byte ptr [rbx + rax], r13b
	adc eax, 2
	and eax, 2048-1
	mov dword ptr [rdx].state.spi_position, eax
	; ------------------

	ret
vera_update_spi_ctrl endp