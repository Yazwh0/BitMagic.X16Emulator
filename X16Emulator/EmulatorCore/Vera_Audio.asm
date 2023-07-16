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

; use r12 for Left
;     r13 for Right

; needs to be called every 512 vera ticks, or ~163.84 cpu ticks
; rax = clock_audionext
vera_render_audio proc
	; calculate next audio clock event
	add rax, 163							; todo: add the .84 to this
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
	movzx r12, byte ptr [rsi + rax]

	inc eax									; step on read
	and eax, VERA_BUFFER_MASK
	mov [rdx].state.pcm_bufferread, eax

	shl r12d, 8								; convert to 16bit space
	imul r12d, [rdx].state.pcm_volume		; apply volume
	shr r12d, 7								; only conisder top bits
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
	movzx r12, byte ptr [rsi + rax]

	inc eax									; step on read
	and eax, VERA_BUFFER_MASK

	shl r12d, 8								; convert to 16bit space
	imul r12d, [rdx].state.pcm_volume		; apply volume
	shr r12d, 7								; only conisder top bits
	mov [rdx].state.pcm_value_l, r12d

	mov r13, r12							; if fifo is empty, then we use previous read
	cmp eax, [rdx].state.pcm_bufferwrite	; if bufferread = bufferwrite then the buffer is empty
	je pcm_8bit_stereo_r_done

	movzx r13, byte ptr [rsi + rax]

	inc eax									; step on read
	and eax, VERA_BUFFER_MASK

	shl r13d, 8								; convert to 16bit space
	imul r13d, [rdx].state.pcm_volume		; apply volume
	shr r13d, 7								; only conisder top bits

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
	movzx r12, byte ptr [rsi + rax]
	shl r12d, 8

	inc eax									; step on read
	and eax, VERA_BUFFER_MASK

	cmp eax, [rdx].state.pcm_bufferwrite	; if bufferread = bufferwrite then the buffer is empty
	je pcm_16bit_mono_noval
	
	or r12b, byte ptr [rsi + rax]

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

	imul r12d, [rdx].state.pcm_volume		; apply volume
	shr r12d, 7								; only conisder top bits
	mov [rdx].state.pcm_value_l, r12d
	mov [rdx].state.pcm_value_r, r12d
	mov r13, r12	

	jmp vera_step_psg

pcm_16bit_stereo:
	mov eax, [rdx].state.pcm_bufferread
	cmp eax, [rdx].state.pcm_bufferwrite	; if bufferread = bufferwrite then the buffer is empty
	je no_data

	mov rsi, [rdx].state.pcm_ptr
	movzx r12, byte ptr [rsi + rax]

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
	shr r12d, 7								; only conisder top bits
	mov [rdx].state.pcm_value_l, r12d
	imul r13d, [rdx].state.pcm_volume		; apply volume
	shr r13d, 7								; only conisder top bits
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


; every render we write to the buffer
vera_write_audiodata:
	mov rsi, [rdx].state.audiooutput_ptr
	mov eax, [rdx].state.audio_write
	mov word ptr [rsi + rax * 4], r12w							; left
	mov word ptr [rsi + rax * 4 + 2], r13w						; right

	clflushopt [rsi + rax * 4]
	clflushopt [rsi + rax * 4 +1]
	clflushopt [rsi + rax * 4 +2]
	clflushopt [rsi + rax * 4 +3]

	inc eax
	and eax, OUTPUT_BUFFER_MASK									; 2meg buffer / 2
	mov dword ptr [rdx].state.audio_write, eax

	clflushopt [rdx].state.audio_write 
	clflushopt [rdx].state.audio_write+1
	clflushopt [rdx].state.audio_write+2
	clflushopt [rdx].state.audio_write+3

	ret	
vera_render_audio endp

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
