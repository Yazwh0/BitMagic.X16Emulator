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



; macro to find the current tile definition, returns data in ax. 
; updates:
; eax: tile information
; r14: current layer tile address
; r11: x
; r12: y
; returns
; eax : tile information
; ebx : tile data
; r8  : -1 if tile complete
; r10 : x position through tile
; r13 : width
; r14 : x mask
get_tile_data macro tile_height, tile_width, colour_depth
	local t_height_px, t_width_px, t_colour_size, t_size_shift, t_tile_mask, t_multiplier, t_colour_mask, t_tile_shift, t_tile_x_mask, t_height_invert_mask, position_found, has_data, test_bypass

	if tile_height eq 0
		t_height_px equ 8
		t_height_invert_mask equ 0111b
	endif
	if tile_height eq 1
		t_height_px equ 16
		t_height_invert_mask equ 01111b
	endif
	if tile_width eq 0
		t_width_px equ 8
		t_multiplier equ 1
	endif
	if tile_width eq 1
		t_width_px equ 16
		t_multiplier equ 2
	endif
	if colour_depth eq 0
		t_colour_size equ 8		
		t_tile_x_mask equ t_width_px-1
	endif
	if colour_depth eq 1
		t_colour_size equ 4
		t_tile_x_mask equ t_width_px-1
	endif
	if colour_depth eq 2
		t_colour_size equ 2
		t_tile_x_mask equ 8-1		; 4bpp always returns a full dword, so 8 pixels
	endif
	if colour_depth eq 3
		t_colour_size equ 1
		t_tile_x_mask equ 4-1		; 8bpp always returns a full dword, so 4 pixels
	endif
	if t_height_px * t_width_px / t_colour_size eq 8
		t_size_shift equ 3
	endif
	if t_height_px * t_width_px / t_colour_size eq 16
		t_size_shift equ 4
	endif
	if t_height_px * t_width_px / t_colour_size eq 32
		t_size_shift equ 5
	endif
	if t_height_px * t_width_px / t_colour_size eq 64
		t_size_shift equ 6
	endif
	if t_height_px * t_width_px / t_colour_size eq 128
		t_size_shift equ 7
	endif
	if t_height_px * t_width_px / t_colour_size eq 256
		t_size_shift equ 8
	endif
	t_tile_mask equ t_height_px - 1
	t_tile_shift equ (t_multiplier - 1) + colour_depth

	mov rsi, [rdx].state.vram_ptr

	xor rbx, rbx

	if colour_depth eq 00
		mov bl, al								; get tile number
	else
		mov bx, ax
		and bx, 01111111111b					; mask off tile index
	endif

	; r12 is y, need to convert it to where the line starts in memory
	if colour_depth ne 0
		bt eax, 11								; check if flipped
		jnc no_v_flip

		xor r12d, t_height_invert_mask			; inverts the y position

		no_v_flip:
	endif
	and r12d, t_tile_mask						; mask for tile height, so now line within tile
	shl r12d, t_tile_shift						; adjust to width of line to get offset address

	shl ebx, t_size_shift						; rbx is now the address
	or ebx, r12d									; adjust to the line offset
	add ebx, r14d								; add to tile base address

	add dword ptr [rdx].state.vram_wait, 1		; for data read

	mov r8d, -1									; no more vram reads for this tile -- gets overwritten later if necessary

	; find dword in memory that is being rendered

	; check if we're part way through a tile, if so store the value for the next render (so vram access is counted) 
	if tile_width eq 1 and colour_depth eq 2
		mov r14d, r11d
		and r14d, 01000b
		cmovz r8d, ebx
	endif
	if tile_width eq 0 and colour_depth eq 3
		mov r14d, r11d
		and r14d, 0111b
		cmp r14d, 0111b - 3
		cmovl r8d, ebx
	endif
	if tile_width eq 1 and colour_depth eq 3
		mov r14d, r11d
		and r14d, 01111b
		cmp r14d, 01111b - 3
		cmovl r8d, ebx
	endif

	; find offset of current x so the render can start from the right position
	mov r10d, r11d							
	mov r14d, t_tile_x_mask						; r14 is a returned value
	and r10d, r14d								; return pixels (offset from boundary)

	if colour_depth ne 0 and colour_depth ne 1
		if tile_width eq 1 or colour_depth eq 2 or colour_depth eq 3
			bt eax, 10
			jnc no_h_flip

			if tile_width eq 1 and colour_depth eq 2
				xor r11d, 01000b							; flip bit to invert, its masked later
			endif
			if tile_width eq 0 and colour_depth eq 3
				xor r11d, 01100b							; flip bit to invert, its masked later
			endif
			if tile_width eq 1 and colour_depth eq 3
				xor r11d, 011100b						; flip bit to invert, its masked later
			endif

		no_h_flip:
		endif
	endif

	if tile_width eq 1 and colour_depth eq 2	; 4bpp
		and r11d, 01000b							; mask x position
		shr r11d, 1								; /2 (ratio for 4bpp to memory) adjust to the actual address
		add ebx, r11d
	endif

	if tile_width eq 0 and colour_depth eq 3
		and r11d, 01100b							; mask x position
		add ebx, r11d
	endif

	if tile_width eq 1 and colour_depth eq 3
		and r11d, 01100b							; mask x position
		add ebx, r11d
	endif

	mov ebx, dword ptr [rsi + rbx]				; set ebx 32bits worth of values

	if colour_depth eq 0 or colour_depth eq 1
		mov r13d, t_width_px						; return width -- not needed for 4 or 8 bpp
	endif

	; now rbx has 32bit from tile data location
	; now r10 has the offset into the tile
	ret
endm


layer_get_tile_proc macro _tile_height, _tile_width, _tile_colour_depth, _tile_map_count
layer_get_tile_&_tile_map_count& proc
	get_tile_data _tile_height, _tile_width, _tile_colour_depth
layer_get_tile_&_tile_map_count& endp
endm

layer_get_tile_proc 0, 0, 0, 0
layer_get_tile_proc 0, 0, 1, 1
layer_get_tile_proc 0, 0, 2, 2
layer_get_tile_proc 0, 0, 3, 3
layer_get_tile_proc 0, 1, 0, 4
layer_get_tile_proc 0, 1, 1, 5
layer_get_tile_proc 0, 1, 2, 6
layer_get_tile_proc 0, 1, 3, 7
layer_get_tile_proc 1, 0, 0, 8
layer_get_tile_proc 1, 0, 1, 9
layer_get_tile_proc 1, 0, 2, 10
layer_get_tile_proc 1, 0, 3, 11
layer_get_tile_proc 1, 1, 0, 12
layer_get_tile_proc 1, 1, 1, 13
layer_get_tile_proc 1, 1, 2, 14
layer_get_tile_proc 1, 1, 3, 15
layer_get_tile_proc 0, 0, 0, 16
layer_get_tile_proc 0, 0, 1, 17
layer_get_tile_proc 0, 0, 2, 18
layer_get_tile_proc 0, 0, 3, 19
layer_get_tile_proc 0, 1, 0, 20
layer_get_tile_proc 0, 1, 1, 21
layer_get_tile_proc 0, 1, 2, 22
layer_get_tile_proc 0, 1, 3, 23
layer_get_tile_proc 1, 0, 0, 24
layer_get_tile_proc 1, 0, 1, 25
layer_get_tile_proc 1, 0, 2, 26
layer_get_tile_proc 1, 0, 3, 27
layer_get_tile_proc 1, 1, 0, 28
layer_get_tile_proc 1, 1, 1, 29
layer_get_tile_proc 1, 1, 2, 30
layer_get_tile_proc 1, 1, 3, 31

align 8
get_tile_jump:
qword layer_get_tile_0 - get_tile_jump
qword layer_get_tile_1 - get_tile_jump
qword layer_get_tile_2 - get_tile_jump
qword layer_get_tile_3 - get_tile_jump
qword layer_get_tile_4 - get_tile_jump
qword layer_get_tile_5 - get_tile_jump
qword layer_get_tile_6 - get_tile_jump
qword layer_get_tile_7 - get_tile_jump
qword layer_get_tile_8 - get_tile_jump
qword layer_get_tile_9 - get_tile_jump
qword layer_get_tile_10 - get_tile_jump
qword layer_get_tile_11 - get_tile_jump
qword layer_get_tile_12 - get_tile_jump
qword layer_get_tile_13 - get_tile_jump
qword layer_get_tile_14 - get_tile_jump
qword layer_get_tile_15 - get_tile_jump
qword layer_get_tile_16 - get_tile_jump
qword layer_get_tile_17 - get_tile_jump
qword layer_get_tile_18 - get_tile_jump
qword layer_get_tile_19 - get_tile_jump
qword layer_get_tile_20 - get_tile_jump
qword layer_get_tile_21 - get_tile_jump
qword layer_get_tile_22 - get_tile_jump
qword layer_get_tile_23 - get_tile_jump
qword layer_get_tile_24 - get_tile_jump
qword layer_get_tile_25 - get_tile_jump
qword layer_get_tile_26 - get_tile_jump
qword layer_get_tile_27 - get_tile_jump
qword layer_get_tile_28 - get_tile_jump
qword layer_get_tile_29 - get_tile_jump
qword layer_get_tile_30 - get_tile_jump
qword layer_get_tile_31 - get_tile_jump
