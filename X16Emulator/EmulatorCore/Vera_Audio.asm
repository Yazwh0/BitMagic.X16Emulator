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

OUTPUT_BUFFER_SIZE equ 200000h	; 2meg per side
OUTPUT_BUFFER_MASK equ OUTPUT_BUFFER_SIZE / 4 - 1 ; 2meg / 2 channels of 2 words

VERA_BUFFER_MASK equ 0fffh		; 4k
VERA_PSG_SIZE_MULT equ 16
VERA_PSG_SIZE equ VERA_PSG_SIZE_MULT * 4

psg_voice struct
	generator	qword ?		; address to jump to in the generate code - so impicit waveform

	waveform	dword ?

	phase		dword ?
	frequency	dword ?
	volume		dword ?
	leftright	dword ?
	widthx		dword ? ; 8

	value		dword ?
	noise		dword ? ; 10

	paddingl_1	qword ? ; 12
	paddingl_2	qword ? ; 14
	paddingl_3  qword ? ; 16

psg_voice ends

vera_psg macro voicenumber
local donothing, render_done, right, done, no_noise_change
	; ---------------------------------
	; calculate new noise
	mov ebx, [rdx].state.psg_noise_signal

	; need to xor bits 1, 2, 4 and 15
	mov ecx, ebx
	shr ecx, 1

	mov edi, ebx
	shr edi, 2
	xor ecx, edi

	shr edi, 2
	xor ecx, edi

	shr edi, 15-4
	xor ecx, edi

	and ecx, 1

	mov edi, ebx
	shl edi, 1
	or ecx, edi
	and ecx, 0ffffh
	mov [rdx].state.psg_noise_signal, ecx

	mov ebx, [rdx].state.psg_noise
	shl ebx, 1
	and ecx, 1
	or ebx, ecx
	and ebx, 111111b
	mov [rdx].state.psg_noise, ebx

	; move on phase
	mov ebx, [rsi].psg_voice.leftright
	test ebx, ebx
	jz donothing

	mov eax, [rsi].psg_voice.phase
	mov ecx, eax
	add eax, [rsi].psg_voice.frequency
	and eax, 1ffffh
	mov [rsi].psg_voice.phase, eax

	jmp [rsi].psg_voice.generator

	psg_&voicenumber&_pulse::
	mov edi, [rsi].psg_voice.widthx
	mov ecx, eax
	shr ecx, 10
	cmp edi, ecx			; set carry if greater (unsigned overflow)
	sbb ecx, ecx			; will be -1 if greater, 0 otherwise
	and ecx, 03fh

	jmp render_done

	psg_&voicenumber&_sawtooth::
	mov ecx, eax
	shr ecx, 11
	xor ecx, [rsi].psg_voice.widthx

	jmp render_done

	psg_&voicenumber&_triangle::
	xor edi, edi
	mov eax, 0ffffh
	test ecx, 10000h		; high bit set? then we're decreasing
	cmovnz edi, eax
	xor ecx, edi
	and ecx, 0ffffh			; mask of top bit
	shr ecx, 10
	xor ecx, [rsi].psg_voice.widthx

	jmp render_done

	psg_&voicenumber&_noise::
	xor eax, ecx
	test eax, 10000h
	jz no_noise_change
	mov ecx, [rdx].state.psg_noise
	mov [rsi].psg_voice.noise, ecx			; store for next sample
	mov edi, [rsi].psg_voice.widthx
	xor edi, 03fh
	and ecx, edi

	jmp render_done

	no_noise_change:
	mov ecx, [rsi].psg_voice.noise
	jmp render_done

	donothing:
	xor ecx, ecx
	mov [rsi].psg_voice.phase, ecx
	jmp done

	render_done:

	; sign extend if 0x20 is set
	;xor ecx, 20h
	;xor edi, edi
	;test ecx, 20h
	;cmove edi, dword ptr psg_mask
	;or ecx, edi
	sub ecx, 32

	imul ecx, [rsi].psg_voice.volume		; apply volume
	test ebx, 1
	je right

	add r12, rcx
	and ebx, 2
	je done

	right:
	add r13, rcx

	done:
	mov [rsi].psg_voice.value, ecx			; store for debugging

	add rsi, VERA_PSG_SIZE
	; ---------------------------------
endm

; use r12 for Left
;     r13 for Right

; 
; Cpu ticks per audio sample :
;
; Cpu Freq / (Vera Freq / Samples)
; 8000000 / (25000000 / 512)
;
; So needs to be called every 512 ver ticks, or 163.84 cpu ticks
; rax = clock_audionext
vera_render_audio proc
	; calculate next audio clock event
	add rax, 163

	mov ebx, [rdx].state.audio_cpu_partial
	add ebx, 84
	cmp ebx, 100
	jl audionext_done

	sub ebx, 100
	inc rax

audionext_done:
	mov [rdx].state.audio_cpu_partial, ebx
	mov [rdx].state.clock_audionext, rax

	; check PCM
	mov eax, [rdx].state.pcm_count
	mov ebx, eax
	add eax, [rdx].state.pcm_samplerate
	mov [rdx].state.pcm_count, eax

	xor eax, ebx
	and eax, 080h			; isolate bit 7, will be set if its changed

	jz vera_step_psg_init

;	PCM needs to set the l and r values bit in register and memory. 
;	The memory copy is for when the sample rate isnt the full 48khz
	lea rax, pcm_modes
	mov ebx, [rdx].state.pcm_mode
	add rax, [rax + rbx * 8]
	jmp rax

pcm_modes:
	qword pcm_8bit_mono - pcm_modes
	qword pcm_8bit_stereo - pcm_modes
	qword pcm_16bit_mono - pcm_modes
	qword pcm_16bit_stereo - pcm_modes

pcm_8bit_mono:
	mov eax, [rdx].state.pcm_bufferread
	cmp eax, [rdx].state.pcm_bufferwrite	; if bufferread = bufferwrite then the buffer is empty
	je no_data

	mov rsi, [rdx].state.pcm_ptr
	movsx r12, byte ptr [rsi + rax]

	inc eax									; step on read
	and eax, VERA_BUFFER_MASK
	mov [rdx].state.pcm_bufferread, eax

	shl r12d, 8								; convert to 16bit space

	mov ecx, [rdx].state.pcm_volume
	imul r12d, [rdx].state.pcm_volume		; apply volume
	shr r12d, 6								; only conisder top bits
	mov [rdx].state.pcm_value_l, r12d
	mov [rdx].state.pcm_value_r, r12d
	mov r13, r12	

	; check if now empty and set flag if so
	mov rsi, [rdx].state.memory_ptr
	mov ebx, 040h
	xor ecx, ecx
	cmp eax, [rdx].state.pcm_bufferwrite	; if bufferread = bufferwrite then the buffer is empty
	cmovne ebx, ecx
	and byte ptr [rsi + AUDIO_CTRL], 03fh	; clear full bit and empty bit
	or byte ptr [rsi + AUDIO_CTRL], bl		; set buffer empty if necessary

	jmp vera_step_psg

pcm_8bit_stereo:
	mov eax, [rdx].state.pcm_bufferread
	cmp eax, [rdx].state.pcm_bufferwrite	; if bufferread = bufferwrite then the buffer is empty
	je no_data

	mov rsi, [rdx].state.pcm_ptr
	movsx r12, byte ptr [rsi + rax]

	inc eax									; step on read
	and eax, VERA_BUFFER_MASK

	shl r12d, 8								; convert to 16bit space
	imul r12d, [rdx].state.pcm_volume		; apply volume
	shr r12d, 6								; only conisder top bits
	mov [rdx].state.pcm_value_l, r12d

	mov r13, r12							; if fifo is empty, then we use previous read
	cmp eax, [rdx].state.pcm_bufferwrite	; if bufferread = bufferwrite then the buffer is empty
	je pcm_8bit_stereo_r_done

	movzx r13, byte ptr [rsi + rax]

	inc eax									; step on read
	and eax, VERA_BUFFER_MASK

	shl r13d, 8								; convert to 16bit space
	imul r13d, [rdx].state.pcm_volume		; apply volume
	shr r13d, 6								; only conisder top bits

pcm_8bit_stereo_r_done:
	mov [rdx].state.pcm_value_r, r13d

	mov [rdx].state.pcm_bufferread, eax

	; check if now empty and set flag if so
	mov rsi, [rdx].state.memory_ptr
	mov ebx, 040h
	xor ecx, ecx
	cmp eax, [rdx].state.pcm_bufferwrite	; if bufferread = bufferwrite then the buffer is empty
	cmovne ebx, ecx
	and byte ptr [rsi + AUDIO_CTRL], 03fh	; clear full bit and empty bit
	or byte ptr [rsi + AUDIO_CTRL], bl		; set buffer empty if necessary

	jmp vera_step_psg

pcm_16bit_mono:
	mov eax, [rdx].state.pcm_bufferread
	cmp eax, [rdx].state.pcm_bufferwrite	; if bufferread = bufferwrite then the buffer is empty
	je no_data

	mov rsi, [rdx].state.pcm_ptr
	movsx r12, byte ptr [rsi + rax]			; first byte is the low part

	inc eax									; step on read
	and eax, VERA_BUFFER_MASK

	cmp eax, [rdx].state.pcm_bufferwrite	; if bufferread = bufferwrite then the buffer is empty
	je pcm_16bit_mono_noval
	
	movzx rbx, byte ptr [rsi + rax]			; second byte is the high part
	shl ebx, 8
	or r12d, ebx

	inc eax									; step on read
	and eax, VERA_BUFFER_MASK

	jmp pcm_16bit_mono_setval

pcm_16bit_mono_noval:
	; r12 has the first low byte set, need to put the same in the high byte
	mov r13, r12
	shl r13d, 8
	or r12d, r13d

pcm_16bit_mono_setval:
	mov [rdx].state.pcm_bufferread, eax
	
	; check if now empty and set flag if so
	mov rsi, [rdx].state.memory_ptr
	mov ebx, 040h
	xor ecx, ecx
	cmp eax, [rdx].state.pcm_bufferwrite	; if bufferread = bufferwrite then the buffer is empty
	cmovne rbx, rcx
	and byte ptr [rsi + AUDIO_CTRL], 03fh	; clear full bit and empty bit
	or byte ptr [rsi + AUDIO_CTRL], bl		; set buffer empty if necessary

	movsx r12, r12w
	imul r12d, [rdx].state.pcm_volume		; apply volume
	shr r12d, 6								; only conisder top bits
	mov [rdx].state.pcm_value_l, r12d
	mov [rdx].state.pcm_value_r, r12d
	mov r13, r12	

	jmp vera_step_psg

pcm_16bit_stereo:
	mov eax, [rdx].state.pcm_bufferread
	cmp eax, [rdx].state.pcm_bufferwrite	; if bufferread = bufferwrite then the buffer is empty
	je no_data

	mov rsi, [rdx].state.pcm_ptr
	movsx r12, byte ptr [rsi + rax]

	inc eax									; step on read
	and eax, VERA_BUFFER_MASK
	
	cmp eax, [rdx].state.pcm_bufferwrite	; if bufferread = bufferwrite then the buffer is empty
	je pcm_16bit_stereo_missing1
	
	movzx rbx, byte ptr [rsi + rax]
	shl ebx, 8
	or r12d, ebx

	inc eax									; step on read
	and eax, VERA_BUFFER_MASK
		
	cmp eax, [rdx].state.pcm_bufferwrite	; if bufferread = bufferwrite then the buffer is empty
	je pcm_16bit_stereo_missing2

	movzx r13, byte ptr [rsi + rax]
	
	inc eax									; step on read
	and eax, VERA_BUFFER_MASK
		
	cmp eax, [rdx].state.pcm_bufferwrite	; if bufferread = bufferwrite then the buffer is empty
	je pcm_16bit_stereo_missing3

	movzx rbx, byte ptr [rsi + rax]
	shl ebx, 8
	or r13d, ebx

	inc eax									; step on read
	and eax, VERA_BUFFER_MASK

	jmp pcm_16bit_stereo_setval

; only read in one byte - r12 only has bottom byte
pcm_16bit_stereo_missing1: 
	mov r13d, r12d
	shl r13d, 8
	or r12d, r13d
	mov r13d, r12d
	jmp pcm_16bit_stereo_setval

; only read in two bytes - r12 is good
pcm_16bit_stereo_missing2:
	mov r13, r12
	jmp pcm_16bit_stereo_setval

; only read in three bytes - r12 is good, r13 only has bottom byte
pcm_16bit_stereo_missing3:
	mov ebx, r13d
	shl ebx, 8
	or r13d, ebx

	jmp pcm_16bit_stereo_setval

pcm_16bit_stereo_setval:

	imul r12d, [rdx].state.pcm_volume		; apply volume
	shr r12d, 6								; only conisder top bits
	mov [rdx].state.pcm_value_l, r12d
	imul r13d, [rdx].state.pcm_volume		; apply volume
	shr r13d, 6								; only conisder top bits
	mov [rdx].state.pcm_value_r, r13d

	; check if now empty and set flag if so
	mov [rdx].state.pcm_bufferread, eax
	mov rsi, [rdx].state.memory_ptr
	mov ebx, 040h
	xor rcx, rcx
	cmp eax, [rdx].state.pcm_bufferwrite	; if bufferread = bufferwrite then the buffer is empty
	cmovne rbx, rcx
	and byte ptr [rsi + AUDIO_CTRL], 03fh	; clear full bit and empty bit
	or byte ptr [rsi + AUDIO_CTRL], bl		; set buffer empty if necessary

	jmp vera_step_psg

no_data:
	xor r12, r12
	xor r13, r13
	mov [rdx].state.pcm_value_l, r12d
	mov [rdx].state.pcm_value_r, r13d

	jmp vera_step_psg

vera_step_psg_init:
	mov r12d, [rdx].state.pcm_value_l		; Set starting values for PSG.
	mov r13d, [rdx].state.pcm_value_r		; Step PCM always sets these

vera_step_psg:
	; AFLOW
	mov eax, [rdx].state.pcm_bufferwrite
	sub eax, [rdx].state.pcm_bufferread
	and eax, 0fffh

	xor ebx, ebx
	mov rcx, 01000b					; AFLOW flag
	cmp eax, 0400h
	cmova ecx, ebx					; clear if greater
	movzx rbx, byte ptr [rsi+ISR]
	and ebx, 011110111b				; clear
	or ebx, ecx						; set if neccesary
	mov byte ptr [rsi+ISR], bl		; write
	shr ecx, 4
	or byte ptr [rdx].state.interrupt, cl

	; now process the PSG voices
	mov rsi, [rdx].state.psg_ptr

	push r8

	xor r8, r8

	vera_psg 0
	vera_psg 1
	vera_psg 2
	vera_psg 3
	vera_psg 4
	vera_psg 5
	vera_psg 6
	vera_psg 7
	vera_psg 8
	vera_psg 9
	vera_psg 10
	vera_psg 11
	vera_psg 12
	vera_psg 13
	vera_psg 14
	vera_psg 15

	pop r8

; every render we write to the buffer
vera_write_audiodata:

	; pull in YM data and mix in
	mov ecx, [rdx].state.ym_left
	add r12w, cx
	mov ecx, [rdx].state.ym_right
	add r13w, cx

	mov rsi, [rdx].state.audiooutput_ptr
	mov eax, [rdx].state.audio_write
	mov word ptr [rsi + rax * 4], r12w							; left
	mov word ptr [rsi + rax * 4 + 2], r13w						; right

	clflushopt [rsi + rax * 4]
	clflushopt [rsi + rax * 4 +1]
	clflushopt [rsi + rax * 4 +2]
	clflushopt [rsi + rax * 4 +3]

	inc eax
	and eax, OUTPUT_BUFFER_MASK									; 2meg buffer / 4
	mov dword ptr [rdx].state.audio_write, eax

	clflushopt [rdx].state.audio_write 
	clflushopt [rdx].state.audio_write+1
	clflushopt [rdx].state.audio_write+2
	clflushopt [rdx].state.audio_write+3

	ret	
vera_render_audio endp

vera_init_psg proc

	mov eax, 16
	mov rsi, [rdx].state.psg_ptr
	xor ecx, ecx

init_loop:
	; set the jump table up for the waveforms passed in
	mov r13d, [rsi].psg_voice.waveform
	add r13d, ecx
	lea rbx, psg_jump
	add rbx, [rbx + r13 * 8]
	mov [rsi].psg_voice.generator, rbx

	add ecx, 4

	add rsi, VERA_PSG_SIZE
	dec eax
	jnz init_loop

	ret
vera_init_psg endp

; Inputs:
; r13b value
; rdi vram address
psg_update_registers proc
	mov rsi, [rdx].state.psg_ptr	

	mov eax, edi
	sub eax, 01f9c0h
	and eax, 111100b		; mask off index
	shl eax, 2+2			; * 16 * 4

	mov ebx, edi
	and ebx, 3				; get byte

	lea r12, psg_jump_table
	add r12, [r12 + rbx * 8]
	jmp r12

psg_jump_table:
	dq psg_byte_0 - psg_jump_table
	dq psg_byte_1 - psg_jump_table
	dq psg_byte_2 - psg_jump_table
	dq psg_byte_3 - psg_jump_table

	psg_byte_0:
	mov r12d, dword ptr [rsi + rax].psg_voice.frequency
	and r12d, 0ff00h
	or r12d, r13d
	mov dword ptr [rsi + rax].psg_voice.frequency, r12d

	ret

	psg_byte_1:
	mov r12d, dword ptr [rsi + rax].psg_voice.frequency
	and r12d, 00ffh
	shl r13d, 8
	or r12d, r13d
	mov dword ptr [rsi + rax].psg_voice.frequency, r12d

	ret

	psg_byte_2:
	; volume
	push r14
	mov r12d, r13d
	and r12d, 03fh
	lea r14, psg_volume
	mov r12d, [r14 + r12 * 4]
	mov dword ptr [rsi + rax].psg_voice.volume, r12d

	; left/right
	shr r13d, 6
	mov dword ptr [rsi + rax].psg_voice.leftright, r13d

	pop r14
	ret

	psg_byte_3:
	; pulse width
	mov r12d, r13d
	and r12d, 03fh
	mov dword ptr [rsi + rax].psg_voice.widthx, r12d

	; waveform
	push r14
	shr r13d, 6
	mov dword ptr [rsi + rax].psg_voice.waveform, r13d		; store for debugging

	; eax is voice * 16 * 4, to index into the structures
	; we need * 4 to index into the jump
	mov r14d, eax
	shr r14d, 4		; / 4 so we can add r13 to get index in the table
	add r14d, r13d
	lea r12, psg_jump
	add r12, [r12 + r14 * 8]
	mov [rsi + rax].psg_voice.generator, r12
	pop r14

	ret

psg_update_registers endp

align 8
psg_mask:
	dword 0ffffffc0h

align 8
psg_jump:
	qword 	psg_0_pulse		- psg_jump
	qword 	psg_0_sawtooth	- psg_jump
	qword 	psg_0_triangle	- psg_jump
	qword 	psg_0_noise		- psg_jump
	qword 	psg_1_pulse		- psg_jump
	qword 	psg_1_sawtooth	- psg_jump
	qword 	psg_1_triangle	- psg_jump
	qword 	psg_1_noise		- psg_jump
	qword 	psg_2_pulse		- psg_jump
	qword 	psg_2_sawtooth	- psg_jump
	qword 	psg_2_triangle	- psg_jump
	qword 	psg_2_noise		- psg_jump
	qword 	psg_3_pulse		- psg_jump
	qword 	psg_3_sawtooth	- psg_jump
	qword 	psg_3_triangle	- psg_jump
	qword 	psg_3_noise		- psg_jump
	qword 	psg_4_pulse		- psg_jump
	qword 	psg_4_sawtooth	- psg_jump
	qword 	psg_4_triangle	- psg_jump
	qword 	psg_4_noise		- psg_jump
	qword 	psg_5_pulse		- psg_jump
	qword 	psg_5_sawtooth	- psg_jump
	qword 	psg_5_triangle	- psg_jump
	qword 	psg_5_noise		- psg_jump
	qword 	psg_6_pulse		- psg_jump
	qword 	psg_6_sawtooth	- psg_jump
	qword 	psg_6_triangle	- psg_jump
	qword 	psg_6_noise		- psg_jump
	qword 	psg_7_pulse		- psg_jump
	qword 	psg_7_sawtooth	- psg_jump
	qword 	psg_7_triangle	- psg_jump
	qword 	psg_7_noise		- psg_jump
	qword 	psg_8_pulse		- psg_jump
	qword 	psg_8_sawtooth	- psg_jump
	qword 	psg_8_triangle	- psg_jump
	qword 	psg_8_noise		- psg_jump
	qword 	psg_9_pulse		- psg_jump
	qword 	psg_9_sawtooth	- psg_jump
	qword 	psg_9_triangle	- psg_jump
	qword 	psg_9_noise		- psg_jump
	qword 	psg_10_pulse	- psg_jump
	qword 	psg_10_sawtooth	- psg_jump
	qword 	psg_10_triangle	- psg_jump
	qword 	psg_10_noise	- psg_jump
	qword 	psg_11_pulse	- psg_jump
	qword 	psg_11_sawtooth	- psg_jump
	qword 	psg_11_triangle	- psg_jump
	qword 	psg_11_noise	- psg_jump
	qword 	psg_12_pulse	- psg_jump
	qword 	psg_12_sawtooth	- psg_jump
	qword 	psg_12_triangle	- psg_jump
	qword 	psg_12_noise	- psg_jump
	qword 	psg_13_pulse	- psg_jump
	qword 	psg_13_sawtooth	- psg_jump
	qword 	psg_13_triangle	- psg_jump
	qword 	psg_13_noise	- psg_jump
	qword 	psg_14_pulse	- psg_jump
	qword 	psg_14_sawtooth	- psg_jump
	qword 	psg_14_triangle	- psg_jump
	qword 	psg_14_noise	- psg_jump
	qword 	psg_15_pulse	- psg_jump
	qword 	psg_15_sawtooth	- psg_jump
	qword 	psg_15_triangle	- psg_jump
	qword 	psg_15_noise	- psg_jump

pcm_volume:
	dword 0
	dword 1
	dword 2
	dword 3
	dword 4
	dword 5
	dword 6
	dword 8
	dword 11
	dword 14
	dword 18
	dword 23
	dword 30
	dword 38
	dword 49
	dword 64


pcm_volume_old:
	dword 0
	dword 2
	dword 4
	dword 6
	dword 8
	dword 10
	dword 12
	dword 16
	dword 21
	dword 27
	dword 35
	dword 45
	dword 59
	dword 76
	dword 99
	dword 128

psg_volume:
	dword 0
	dword 1
	dword 1
	dword 1
	dword 2
	dword 2
	dword 2
	dword 2
	dword 2
	dword 2
	dword 2
	dword 3
	dword 3
	dword 3
	dword 3
	dword 3
	dword 4
	dword 4
	dword 4
	dword 4
	dword 5
	dword 5
	dword 5
	dword 6
	dword 6
	dword 7
	dword 7
	dword 7
	dword 8
	dword 8
	dword 9
	dword 9
	dword 10
	dword 11
	dword 11
	dword 12
	dword 13
	dword 14
	dword 14
	dword 15
	dword 16
	dword 17
	dword 18
	dword 19
	dword 21
	dword 22
	dword 23
	dword 25
	dword 26
	dword 28
	dword 29
	dword 31
	dword 33
	dword 35
	dword 37
	dword 39
	dword 42
	dword 44
	dword 47
	dword 50
	dword 52
	dword 56
	dword 59
	dword 63

