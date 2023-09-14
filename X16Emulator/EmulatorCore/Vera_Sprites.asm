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

SPRITE_LEN equ 64

SPRITE_SEARCHING equ 0
SPRITE_RENDERING equ 1
SPRITE_DONE equ 2

SPRITE_COUNT equ 128

sprite struct
	address dword ?
	palette_offset dword ?
	collision_mask dword ?
	widthx dword ? ; width is a keyword apparently
	height dword ?
	x dword ?
	y dword ?
	mode dword ?	; mode, height, width, vflip, hflip : 7 bits.
	depth dword ?

	padding_1 dword ?
	padding_2 qword ?
	padding_3 qword ?
	padding_4 qword ?

sprite ends

; r12 actual line (w/ v_scroll)
sprites_render proc
	mov eax, dword ptr [rdx].state.sprite_render_mode
	test rax, rax
	jz sprites_render_find


	mov eax, dword ptr [rdx].state.vram_wait	; cant render anything while waiting on the bus.
	test eax, eax
	jnz skip

	mov eax, dword ptr [rdx].state.sprite_position
	mov ebx, dword ptr [rdx].state.sprite_width
	mov rsi, qword ptr [rdx].state.display_buffer_ptr

	jmp qword ptr [rdx].state.sprite_jmp

skip:
	ret
sprites_render endp

; check current sprite to see if its to be rendered
sprites_render_find proc

	mov eax, dword ptr [rdx].state.sprite_position

	add eax, 1
	cmp eax, SPRITE_COUNT
	je all_done

	mov dword ptr [rdx].state.sprite_position, eax

	shl rax, 6	; * 64
	mov rsi, [rdx].state.sprite_ptr

	mov ebx, dword ptr [rsi + rax].sprite.depth	; if zero, the sprite is disabled.
	test ebx, ebx
	jz not_on_line

	; check if sprite is on line
	mov ebx, dword ptr [rsi + rax].sprite.y
	cmp r12d, ebx
	jl not_on_line

	add ebx, dword ptr [rsi + rax].sprite.height
	cmp r12d, ebx
	jge not_on_line

	; found a sprite!
	mov dword ptr [rdx].state.sprite_render_mode, SPRITE_RENDERING 
	mov dword ptr [rdx].state.sprite_width, 0

	; snap various sprite attributes
	mov ebx, dword ptr [rsi + rax].sprite.depth	
	mov dword ptr [rdx].state.sprite_depth, ebx

	mov ebx, dword ptr [rsi + rax].sprite.collision_mask
	mov dword ptr [rdx].state.sprite_collision_mask, ebx
	
	mov ebx, dword ptr [rsi + rax].sprite.x
	mov dword ptr [rdx].state.sprite_x, ebx

	mov ebx, dword ptr [rsi + rax].sprite.y
	mov dword ptr [rdx].state.sprite_y, ebx

	mov ebx, dword ptr [rsi + rax].sprite.mode
	
	lea rax, sprite_definition_jump
	add rax, [rax + rbx * 8]

	mov qword ptr [rdx].state.sprite_jmp, rax
	mov dword ptr [rdx].state.sprite_wait, 2
	ret
	
not_on_line:
	;mov dword ptr [rdx].state.sprite_wait, 1 ; needs checking
	ret

all_done:
	mov dword ptr [rdx].state.sprite_render_mode, SPRITE_DONE	; all done
	mov dword ptr [rdx].state.sprite_wait, 0ffffh				; enough to get to the end of the line
	ret
sprites_render_find endp

; Inputs:
; r13b value
; rdi vram address
sprite_update_registers proc

	; keep sprite data in line with vram
	mov rsi, [rdx].state.sprite_ptr
	
	; sprites are 8bytes, find sprite index
	mov rax, rdi
	sub rax, 1fc00h
	and rax, 001111111000b	; mask off index (128 sprites)
	shl rax, 6-3			; adust to actual offset

	; set rbx to the index of what was changed
	mov rbx, rdi
	and rbx, 07h 

	lea r12, sprite_jump_table
	add r12, [r12 + rbx * 8]
	jmp r12

sprite_jump_table:
	dq sprite_byte_0 - sprite_jump_table
	dq sprite_byte_1 - sprite_jump_table
	dq sprite_byte_2 - sprite_jump_table
	dq sprite_byte_3 - sprite_jump_table
	dq sprite_byte_4 - sprite_jump_table
	dq sprite_byte_5 - sprite_jump_table
	dq sprite_byte_6 - sprite_jump_table
	dq sprite_byte_7 - sprite_jump_table

; Address Low
sprite_byte_0:
	shl r13, 5
	mov r12d, dword ptr [rsi + rax].sprite.address
	and r12, 1e000h ; bits 16-13
	or r12, r13
	mov dword ptr [rsi + rax].sprite.address, r12d

	ret

; Mode + Address H
sprite_byte_1:
	mov r12, r13
	and r12, 10000000b	; islate mode
	shr r12, 1			; move to destination
	mov ebx, dword ptr [rsi + rax].sprite.mode
	and rbx, 00111111b	; mask
	or rbx, r12
	mov dword ptr [rsi + rax].sprite.mode, ebx

	and r13, 01111b
	shl r13, 13
	mov r12d, dword ptr [rsi + rax].sprite.address
	and r12, 01111111100000b ; bits 12-5
	or r12, r13
	mov dword ptr [rsi + rax].sprite.address, r12d

	ret

; X Low
sprite_byte_2:
	mov r12d, dword ptr [rsi + rax].sprite.x
	and r12d, 0300h
	or r12, r13
	mov dword ptr [rsi + rax].sprite.x, r12d

	ret

; X High
sprite_byte_3:
	and r13, 03h
	; test high bit, and extend if necessary so we have a signed dword
	mov r12, 0fffffch ; 11111100b
	xor rbx, rbx
	cmp r13, 03h
	cmovne r12, rbx
	or r13, r12

	shl r13, 8

	mov r12d, dword ptr [rsi + rax].sprite.x
	and r12d, 00ffh
	or r12, r13
	mov dword ptr [rsi + rax].sprite.x, r12d
	
	ret

; Y Low
sprite_byte_4:
	mov r12d, dword ptr [rsi + rax].sprite.y
	and r12d, 0300h
	or r12, r13
	mov dword ptr [rsi + rax].sprite.y, r12d

	ret

; Y High
sprite_byte_5:
	and r13, 03h
	; test high bit, and extend if necessary so we have a signed dword
	mov r12, 0fffffch ; 11111100b
	xor rbx, rbx
	bt r13, 1
	cmovnc r12, rbx
	or r13, r12

	shl r13, 8

	mov r12d, dword ptr [rsi + rax].sprite.y
	and r12d, 00ffh
	or r12, r13
	mov dword ptr [rsi + rax].sprite.y, r12d

	ret

sprite_byte_6:
	; store depth
	mov r12, r13
	and r12, 1100b
	shr r12, 2
	mov dword ptr [rsi + rax].sprite.depth, r12d

	; store collision mask
	mov r12, r13
	and r12, 11110000b
	shr r12, 4
	mov dword ptr [rsi + rax].sprite.collision_mask, r12d

	mov r12, r13
	and r12, 00000011b
	mov ebx, dword ptr [rsi + rax].sprite.mode
	and rbx, 11111100b	; mask
	or rbx, r12

	mov dword ptr [rsi + rax].sprite.mode, ebx

	ret

sprite_byte_7:
	; store palette offset
	mov r12, r13
	and r12, 1111b
	mov dword ptr [rsi + rax].sprite.palette_offset, r12d

	push rcx
	mov rcx, r13
	and rcx, 11000000b
	shr rcx, 6
	mov rbx, 8
	shl rbx, cl
	mov dword ptr [rsi + rax].sprite.height, ebx
	pop rcx

	push rcx
	mov rcx, r13
	and rcx, 110000b
	shr rcx, 4
	mov rbx, 8
	shl rbx, cl
	mov dword ptr [rsi + rax].sprite.widthx, ebx
	pop rcx

	; store height change for render mode
	and r13, 11110000b
	shr r13, 2
	mov ebx, dword ptr [rsi + rax].sprite.mode
	and rbx, 01000011b	; mask
	or rbx, r13
	mov dword ptr [rsi + rax].sprite.mode, ebx

exit:
	ret

sprite_update_registers endp

; r14 : pixel data
; rdi : sprite collision_mask
; rsi : display buffer
; r8  : sprite depth
; r13 : output x
render_pixel_data macro mask, shift, pixeladd
	local dont_draw, no_value, sprite_collision
	mov r14d, r12d
	and r14d, mask

	if shift ne 0
		shr r14d, shift
	endif

	; dont write blank pixels
	jz dont_draw

	; ensure output pixel is in visible bounds
	; we do this in parts as r13 can be either extended to go into the buffer (opposite lines)
	; or negative if off screen.
	;int 3
	mov rax, r13
	if pixeladd ne 0
		add eax, pixeladd
	endif

	test eax, eax			 ; off the screen to the left?
	js dont_draw

	and eax, 0011111111111b  ; contrstain to a single line (as r13 the line buffer, which is double buffered)

	cmp eax, VISIBLE_WIDTH-1 ; off the screen to the right or left?
	ja dont_draw
	
	; can only write on pixels that are the same depth or lower
	movzx rax, byte ptr [rsi + r13 + pixeladd + BUFFER_SPRITE_DEPTH]
	cmp r8d, eax
	jl sprite_collision

	; if there is something written, dont overwrite
	movzx rax, byte ptr [rsi + r13 + pixeladd + BUFFER_SPRITE_VALUE]
	test eax, eax
	jnz sprite_collision

	lea eax, [r14 + r10] ; colour if we apply palette shift
	lea ebx, [r14 - 1]	 ; 0-16 -> -1-15, so we can test on 0-15.
	cmp ebx, 16
	cmovb r14d, eax

	mov byte ptr [rsi + r13 + pixeladd + BUFFER_SPRITE_VALUE], r14b
	mov byte ptr [rsi + r13 + pixeladd + BUFFER_SPRITE_DEPTH], r8b

	sprite_collision:
	; sprite collision
	; or on the collision to the line buffer
	movzx rax, byte ptr [rsi + r13 + pixeladd + BUFFER_SPRITE_COL]
	mov ebx, eax

	; write the or'd value to the frame buffer
	or eax, edi
	mov byte ptr[rsi + r13 + pixeladd + BUFFER_SPRITE_COL], al

	; to check for a collision that can fire IRQ we and with the sprite's mask and or that onto the frame collision value
	and ebx, edi
	or dword ptr [rdx].state.frame_sprite_collision, ebx

	dont_draw:
endm

; render a sprite to the sprite display buffer
; expects 
; rax : sprite number
; rbx : width so far
; rsi : display buffer
; r12 : line
; r15 : buffer offset
render_sprite macro bpp, inp_height, inp_width, vflip, hflip
	local sprite_width_px, exit, skip 

	shl rax, 6

	; fetch 32bits of data
	mov rdi, qword ptr [rdx].state.sprite_ptr
	mov r13d, dword ptr [rdi + rax].sprite.address	; get address
	mov r10d, dword ptr [rdi + rax].sprite.palette_offset
	shl r10, 4	; todo: should be stored in this form.
	mov edi, dword ptr [rdi + rax].sprite.collision_mask

	mov r8d, dword ptr [rdx].state.sprite_depth

	; calculate offset
	mov r14d, dword ptr [rdx].state.sprite_y	; this shouldn't ever result in a negative position
	sub r12d, r14d

	if vflip eq 1
		if inp_height eq 0
			mov r14d, 7
		endif
		if inp_height eq 1
			mov r14d, 15
		endif
		if inp_height eq 2
			mov r14d, 31
		endif
		if inp_height eq 3
			mov r14d, 63
		endif
		sub r14d, r12d
		mov r12d, r14d
	endif

	; adjust for line
	; assume we're in 8bpp
	if inp_width eq 0
		sprite_width_px equ 8
		shl r12, 3
	endif
	if inp_width eq 1
		sprite_width_px equ 16
		shl r12, 4
	endif
	if inp_width eq 2
		sprite_width_px equ 32
		shl r12, 5
	endif
	if inp_width eq 3
		sprite_width_px equ 64
		shl r12, 6
	endif

	if hflip eq 0
		add r12d, ebx		; add on position within sprite
	endif

	if hflip eq 1
		if bpp eq 0
			; rbx is 0, 4, 8, 12, etc... so need to inverse
			; minus the width -8, -8 as 4 is the size for one read.
			if inp_width eq 0
				mov r14d, 8-8
			endif
			if inp_width eq 1
				mov r14d, 16-8
			endif
			if inp_width eq 2
				mov r14d, 32-8
			endif
			if inp_width eq 3
				mov r14d, 64-8
			endif

			sub r14d, ebx

			add r12d, r14d		; add on position within sprite
		endif

		if bpp eq 1
			; rbx is 0, 4, 8, 12, etc... so need to inverse
			; minus the width -4, -4 as 4 is the size for one read.
			if inp_width eq 0
				mov r14d, 8-4
			endif
			if inp_width eq 1
				mov r14d, 16-4
			endif
			if inp_width eq 2
				mov r14d, 32-4
			endif
			if inp_width eq 3
				mov r14d, 64-4
			endif

		sub r14d, ebx

		add r12d, r14d		; add on position within sprite
		endif
	endif

	; if we're 4bpp, then adjust
	if bpp eq 0
		shr r12, 1
	endif

	add r12d, r13d		; add on vram start

	; set r13 to the output x
	movsxd r13, dword ptr [rdx].state.sprite_x
	add r13, rbx
	add r13, r15		; add offset in buffer

	push rdi
	mov rdi, qword ptr [rdx].state.vram_ptr
	mov r12d, dword ptr [rdi + r12]	; get data
	pop rdi
	
	push rbx
	if bpp eq 0		
		; display pixels
		if hflip eq 0
			render_pixel_data 0000000f0h, 04, 0
			render_pixel_data 00000000fh, 00, 1
			render_pixel_data 00000f000h, 12, 2
			render_pixel_data 000000f00h, 08, 3
			render_pixel_data 000f00000h, 20, 4
			render_pixel_data 0000f0000h, 16, 5
			render_pixel_data 0f0000000h, 28, 6
			render_pixel_data 00f000000h, 24, 7
		endif

		if hflip eq 1
			render_pixel_data 00f000000h, 24, 0
			render_pixel_data 0f0000000h, 28, 1
			render_pixel_data 0000f0000h, 16, 2
			render_pixel_data 000f00000h, 20, 3
			render_pixel_data 000000f00h, 08, 4
			render_pixel_data 00000f000h, 12, 5
			render_pixel_data 00000000fh, 00, 6
			render_pixel_data 0000000f0h, 04, 7
		endif

		pop rbx
		add rbx, 8
		mov dword ptr [rdx].state.sprite_wait, 8+1 ; +1 for post vram read cycle
	endif
	if bpp eq 1
		; display pixels
		if hflip eq 0
			render_pixel_data 0000000ffh, 00, 0
			render_pixel_data 00000ff00h, 08, 1
			render_pixel_data 000ff0000h, 16, 2
			render_pixel_data 0ff000000h, 24, 3
		endif

		if hflip eq 1
			render_pixel_data 0ff000000h, 24, 0
			render_pixel_data 000ff0000h, 16, 1
			render_pixel_data 00000ff00h, 08, 2
			render_pixel_data 0000000ffh, 00, 3
		endif

		pop rbx
		add rbx, 4
		mov dword ptr [rdx].state.sprite_wait, 4+1 ; +1 for post vram read cycle
	endif

	cmp ebx, sprite_width_px
	jne exit

sprite_finished:
	mov dword ptr [rdx].state.sprite_render_mode, 0 ; back to seaching
	mov dword ptr [rdx].state.sprite_width, 0
	ret
exit:
	mov dword ptr [rdx].state.sprite_width, ebx
	ret
endm

sprite_definition_proc macro _sprite_bpp, _sprite_height, _sprite_width, _vflip, _hflip, _sprite_definition_count
sprite_definition_&_sprite_definition_count& proc
	render_sprite _sprite_bpp, _sprite_height, _sprite_width, _vflip, _hflip
sprite_definition_&_sprite_definition_count& endp
endm

sprite_definition_proc 0, 0, 0, 0, 0, 0
sprite_definition_proc 0, 0, 0, 0, 1, 1
sprite_definition_proc 0, 0, 0, 1, 0, 2
sprite_definition_proc 0, 0, 0, 1, 1, 3
sprite_definition_proc 0, 0, 1, 0, 0, 4
sprite_definition_proc 0, 0, 1, 0, 1, 5
sprite_definition_proc 0, 0, 1, 1, 0, 6
sprite_definition_proc 0, 0, 1, 1, 1, 7
sprite_definition_proc 0, 0, 2, 0, 0, 8
sprite_definition_proc 0, 0, 2, 0, 1, 9
sprite_definition_proc 0, 0, 2, 1, 0, 10
sprite_definition_proc 0, 0, 2, 1, 1, 11
sprite_definition_proc 0, 0, 3, 0, 0, 12
sprite_definition_proc 0, 0, 3, 0, 1, 13
sprite_definition_proc 0, 0, 3, 1, 0, 14
sprite_definition_proc 0, 0, 3, 1, 1, 15
sprite_definition_proc 0, 1, 0, 0, 0, 16
sprite_definition_proc 0, 1, 0, 0, 1, 17
sprite_definition_proc 0, 1, 0, 1, 0, 18
sprite_definition_proc 0, 1, 0, 1, 1, 19
sprite_definition_proc 0, 1, 1, 0, 0, 20
sprite_definition_proc 0, 1, 1, 0, 1, 21
sprite_definition_proc 0, 1, 1, 1, 0, 22
sprite_definition_proc 0, 1, 1, 1, 1, 23
sprite_definition_proc 0, 1, 2, 0, 0, 24
sprite_definition_proc 0, 1, 2, 0, 1, 25
sprite_definition_proc 0, 1, 2, 1, 0, 26
sprite_definition_proc 0, 1, 2, 1, 1, 27
sprite_definition_proc 0, 1, 3, 0, 0, 28
sprite_definition_proc 0, 1, 3, 0, 1, 29
sprite_definition_proc 0, 1, 3, 1, 0, 30
sprite_definition_proc 0, 1, 3, 1, 1, 31
sprite_definition_proc 0, 2, 0, 0, 0, 32
sprite_definition_proc 0, 2, 0, 0, 1, 33
sprite_definition_proc 0, 2, 0, 1, 0, 34
sprite_definition_proc 0, 2, 0, 1, 1, 35
sprite_definition_proc 0, 2, 1, 0, 0, 36
sprite_definition_proc 0, 2, 1, 0, 1, 37
sprite_definition_proc 0, 2, 1, 1, 0, 38
sprite_definition_proc 0, 2, 1, 1, 1, 39
sprite_definition_proc 0, 2, 2, 0, 0, 40
sprite_definition_proc 0, 2, 2, 0, 1, 41
sprite_definition_proc 0, 2, 2, 1, 0, 42
sprite_definition_proc 0, 2, 2, 1, 1, 43
sprite_definition_proc 0, 2, 3, 0, 0, 44
sprite_definition_proc 0, 2, 3, 0, 1, 45
sprite_definition_proc 0, 2, 3, 1, 0, 46
sprite_definition_proc 0, 2, 3, 1, 1, 47
sprite_definition_proc 0, 3, 0, 0, 0, 48
sprite_definition_proc 0, 3, 0, 0, 1, 49
sprite_definition_proc 0, 3, 0, 1, 0, 50
sprite_definition_proc 0, 3, 0, 1, 1, 51
sprite_definition_proc 0, 3, 1, 0, 0, 52
sprite_definition_proc 0, 3, 1, 0, 1, 53
sprite_definition_proc 0, 3, 1, 1, 0, 54
sprite_definition_proc 0, 3, 1, 1, 1, 55
sprite_definition_proc 0, 3, 2, 0, 0, 56
sprite_definition_proc 0, 3, 2, 0, 1, 57
sprite_definition_proc 0, 3, 2, 1, 0, 58
sprite_definition_proc 0, 3, 2, 1, 1, 59
sprite_definition_proc 0, 3, 3, 0, 0, 60
sprite_definition_proc 0, 3, 3, 0, 1, 61
sprite_definition_proc 0, 3, 3, 1, 0, 62
sprite_definition_proc 0, 3, 3, 1, 1, 63
sprite_definition_proc 1, 0, 0, 0, 0, 64
sprite_definition_proc 1, 0, 0, 0, 1, 65
sprite_definition_proc 1, 0, 0, 1, 0, 66
sprite_definition_proc 1, 0, 0, 1, 1, 67
sprite_definition_proc 1, 0, 1, 0, 0, 68
sprite_definition_proc 1, 0, 1, 0, 1, 69
sprite_definition_proc 1, 0, 1, 1, 0, 70
sprite_definition_proc 1, 0, 1, 1, 1, 71
sprite_definition_proc 1, 0, 2, 0, 0, 72
sprite_definition_proc 1, 0, 2, 0, 1, 73
sprite_definition_proc 1, 0, 2, 1, 0, 74
sprite_definition_proc 1, 0, 2, 1, 1, 75
sprite_definition_proc 1, 0, 3, 0, 0, 76
sprite_definition_proc 1, 0, 3, 0, 1, 77
sprite_definition_proc 1, 0, 3, 1, 0, 78
sprite_definition_proc 1, 0, 3, 1, 1, 79
sprite_definition_proc 1, 1, 0, 0, 0, 80
sprite_definition_proc 1, 1, 0, 0, 1, 81
sprite_definition_proc 1, 1, 0, 1, 0, 82
sprite_definition_proc 1, 1, 0, 1, 1, 83
sprite_definition_proc 1, 1, 1, 0, 0, 84
sprite_definition_proc 1, 1, 1, 0, 1, 85
sprite_definition_proc 1, 1, 1, 1, 0, 86
sprite_definition_proc 1, 1, 1, 1, 1, 87
sprite_definition_proc 1, 1, 2, 0, 0, 88
sprite_definition_proc 1, 1, 2, 0, 1, 89
sprite_definition_proc 1, 1, 2, 1, 0, 90
sprite_definition_proc 1, 1, 2, 1, 1, 91
sprite_definition_proc 1, 1, 3, 0, 0, 92
sprite_definition_proc 1, 1, 3, 0, 1, 93
sprite_definition_proc 1, 1, 3, 1, 0, 94
sprite_definition_proc 1, 1, 3, 1, 1, 95
sprite_definition_proc 1, 2, 0, 0, 0, 96
sprite_definition_proc 1, 2, 0, 0, 1, 97
sprite_definition_proc 1, 2, 0, 1, 0, 98
sprite_definition_proc 1, 2, 0, 1, 1, 99
sprite_definition_proc 1, 2, 1, 0, 0, 100
sprite_definition_proc 1, 2, 1, 0, 1, 101
sprite_definition_proc 1, 2, 1, 1, 0, 102
sprite_definition_proc 1, 2, 1, 1, 1, 103
sprite_definition_proc 1, 2, 2, 0, 0, 104
sprite_definition_proc 1, 2, 2, 0, 1, 105
sprite_definition_proc 1, 2, 2, 1, 0, 106
sprite_definition_proc 1, 2, 2, 1, 1, 107
sprite_definition_proc 1, 2, 3, 0, 0, 108
sprite_definition_proc 1, 2, 3, 0, 1, 109
sprite_definition_proc 1, 2, 3, 1, 0, 110
sprite_definition_proc 1, 2, 3, 1, 1, 111
sprite_definition_proc 1, 3, 0, 0, 0, 112
sprite_definition_proc 1, 3, 0, 0, 1, 113
sprite_definition_proc 1, 3, 0, 1, 0, 114
sprite_definition_proc 1, 3, 0, 1, 1, 115
sprite_definition_proc 1, 3, 1, 0, 0, 116
sprite_definition_proc 1, 3, 1, 0, 1, 117
sprite_definition_proc 1, 3, 1, 1, 0, 118
sprite_definition_proc 1, 3, 1, 1, 1, 119
sprite_definition_proc 1, 3, 2, 0, 0, 120
sprite_definition_proc 1, 3, 2, 0, 1, 121
sprite_definition_proc 1, 3, 2, 1, 0, 122
sprite_definition_proc 1, 3, 2, 1, 1, 123
sprite_definition_proc 1, 3, 3, 0, 0, 124
sprite_definition_proc 1, 3, 3, 0, 1, 125
sprite_definition_proc 1, 3, 3, 1, 0, 126
sprite_definition_proc 1, 3, 3, 1, 1, 127

align 8
sprite_definition_jump:
	qword sprite_definition_0 - sprite_definition_jump
	qword sprite_definition_1 - sprite_definition_jump
	qword sprite_definition_2 - sprite_definition_jump
	qword sprite_definition_3 - sprite_definition_jump
	qword sprite_definition_4 - sprite_definition_jump
	qword sprite_definition_5 - sprite_definition_jump
	qword sprite_definition_6 - sprite_definition_jump
	qword sprite_definition_7 - sprite_definition_jump
	qword sprite_definition_8 - sprite_definition_jump
	qword sprite_definition_9 - sprite_definition_jump
	qword sprite_definition_10 - sprite_definition_jump
	qword sprite_definition_11 - sprite_definition_jump
	qword sprite_definition_12 - sprite_definition_jump
	qword sprite_definition_13 - sprite_definition_jump
	qword sprite_definition_14 - sprite_definition_jump
	qword sprite_definition_15 - sprite_definition_jump
	qword sprite_definition_16 - sprite_definition_jump
	qword sprite_definition_17 - sprite_definition_jump
	qword sprite_definition_18 - sprite_definition_jump
	qword sprite_definition_19 - sprite_definition_jump
	qword sprite_definition_20 - sprite_definition_jump
	qword sprite_definition_21 - sprite_definition_jump
	qword sprite_definition_22 - sprite_definition_jump
	qword sprite_definition_23 - sprite_definition_jump
	qword sprite_definition_24 - sprite_definition_jump
	qword sprite_definition_25 - sprite_definition_jump
	qword sprite_definition_26 - sprite_definition_jump
	qword sprite_definition_27 - sprite_definition_jump
	qword sprite_definition_28 - sprite_definition_jump
	qword sprite_definition_29 - sprite_definition_jump
	qword sprite_definition_30 - sprite_definition_jump
	qword sprite_definition_31 - sprite_definition_jump
	qword sprite_definition_32 - sprite_definition_jump
	qword sprite_definition_33 - sprite_definition_jump
	qword sprite_definition_34 - sprite_definition_jump
	qword sprite_definition_35 - sprite_definition_jump
	qword sprite_definition_36 - sprite_definition_jump
	qword sprite_definition_37 - sprite_definition_jump
	qword sprite_definition_38 - sprite_definition_jump
	qword sprite_definition_39 - sprite_definition_jump
	qword sprite_definition_40 - sprite_definition_jump
	qword sprite_definition_41 - sprite_definition_jump
	qword sprite_definition_42 - sprite_definition_jump
	qword sprite_definition_43 - sprite_definition_jump
	qword sprite_definition_44 - sprite_definition_jump
	qword sprite_definition_45 - sprite_definition_jump
	qword sprite_definition_46 - sprite_definition_jump
	qword sprite_definition_47 - sprite_definition_jump
	qword sprite_definition_48 - sprite_definition_jump
	qword sprite_definition_49 - sprite_definition_jump
	qword sprite_definition_50 - sprite_definition_jump
	qword sprite_definition_51 - sprite_definition_jump
	qword sprite_definition_52 - sprite_definition_jump
	qword sprite_definition_53 - sprite_definition_jump
	qword sprite_definition_54 - sprite_definition_jump
	qword sprite_definition_55 - sprite_definition_jump
	qword sprite_definition_56 - sprite_definition_jump
	qword sprite_definition_57 - sprite_definition_jump
	qword sprite_definition_58 - sprite_definition_jump
	qword sprite_definition_59 - sprite_definition_jump
	qword sprite_definition_60 - sprite_definition_jump
	qword sprite_definition_61 - sprite_definition_jump
	qword sprite_definition_62 - sprite_definition_jump
	qword sprite_definition_63 - sprite_definition_jump
	qword sprite_definition_64 - sprite_definition_jump
	qword sprite_definition_65 - sprite_definition_jump
	qword sprite_definition_66 - sprite_definition_jump
	qword sprite_definition_67 - sprite_definition_jump
	qword sprite_definition_68 - sprite_definition_jump
	qword sprite_definition_69 - sprite_definition_jump
	qword sprite_definition_70 - sprite_definition_jump
	qword sprite_definition_71 - sprite_definition_jump
	qword sprite_definition_72 - sprite_definition_jump
	qword sprite_definition_73 - sprite_definition_jump
	qword sprite_definition_74 - sprite_definition_jump
	qword sprite_definition_75 - sprite_definition_jump
	qword sprite_definition_76 - sprite_definition_jump
	qword sprite_definition_77 - sprite_definition_jump
	qword sprite_definition_78 - sprite_definition_jump
	qword sprite_definition_79 - sprite_definition_jump
	qword sprite_definition_80 - sprite_definition_jump
	qword sprite_definition_81 - sprite_definition_jump
	qword sprite_definition_82 - sprite_definition_jump
	qword sprite_definition_83 - sprite_definition_jump
	qword sprite_definition_84 - sprite_definition_jump
	qword sprite_definition_85 - sprite_definition_jump
	qword sprite_definition_86 - sprite_definition_jump
	qword sprite_definition_87 - sprite_definition_jump
	qword sprite_definition_88 - sprite_definition_jump
	qword sprite_definition_89 - sprite_definition_jump
	qword sprite_definition_90 - sprite_definition_jump
	qword sprite_definition_91 - sprite_definition_jump
	qword sprite_definition_92 - sprite_definition_jump
	qword sprite_definition_93 - sprite_definition_jump
	qword sprite_definition_94 - sprite_definition_jump
	qword sprite_definition_95 - sprite_definition_jump
	qword sprite_definition_96 - sprite_definition_jump
	qword sprite_definition_97 - sprite_definition_jump
	qword sprite_definition_98 - sprite_definition_jump
	qword sprite_definition_99 - sprite_definition_jump
	qword sprite_definition_100 - sprite_definition_jump
	qword sprite_definition_101 - sprite_definition_jump
	qword sprite_definition_102 - sprite_definition_jump
	qword sprite_definition_103 - sprite_definition_jump
	qword sprite_definition_104 - sprite_definition_jump
	qword sprite_definition_105 - sprite_definition_jump
	qword sprite_definition_106 - sprite_definition_jump
	qword sprite_definition_107 - sprite_definition_jump
	qword sprite_definition_108 - sprite_definition_jump
	qword sprite_definition_109 - sprite_definition_jump
	qword sprite_definition_110 - sprite_definition_jump
	qword sprite_definition_111 - sprite_definition_jump
	qword sprite_definition_112 - sprite_definition_jump
	qword sprite_definition_113 - sprite_definition_jump
	qword sprite_definition_114 - sprite_definition_jump
	qword sprite_definition_115 - sprite_definition_jump
	qword sprite_definition_116 - sprite_definition_jump
	qword sprite_definition_117 - sprite_definition_jump
	qword sprite_definition_118 - sprite_definition_jump
	qword sprite_definition_119 - sprite_definition_jump
	qword sprite_definition_120 - sprite_definition_jump
	qword sprite_definition_121 - sprite_definition_jump
	qword sprite_definition_122 - sprite_definition_jump
	qword sprite_definition_123 - sprite_definition_jump
	qword sprite_definition_124 - sprite_definition_jump
	qword sprite_definition_125 - sprite_definition_jump
	qword sprite_definition_126 - sprite_definition_jump
	qword sprite_definition_127 - sprite_definition_jump
