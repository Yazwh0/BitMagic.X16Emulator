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
; r13: current layer map address
; r11: x
; r12: y
; returns
; eax: tile information
; rbx: tile count, 1 if both should be used, 0 if only ax (for scrolling offset)

get_map_data macro map_height, map_width, tile_height, tile_width
	local m_height_px, m_width_px

	if map_height eq 0
		m_height_px equ 32
	endif
	if map_height eq 1
		m_height_px equ 64
	endif
	if map_height eq 2
		m_height_px equ 128
	endif
	if map_height eq 3
		m_height_px equ 256
	endif

	if map_width eq 0
		m_width_px equ 32
	endif
	if map_width eq 1
		m_width_px equ 64
	endif
	if map_width eq 2
		m_width_px equ 128
	endif
	if map_width eq 3
		m_width_px equ 256
	endif

	mov rsi, [rdx].state.vram_ptr

	add dword ptr[rdx].state.vram_wait, 1 ; for tile map read

	mov eax, r12d					; y
	shr eax, tile_height + 3		; / tile height
	and eax, m_height_px - 1		; constrain to map
	shl eax, map_width + 5			; *map width

	mov ebx, r11d					; x
	shr ebx, tile_width + 3			; / tile width
	and ebx, m_width_px - 1			; constrain to map
	add eax, ebx

	mov ebx, eax					; return alignment
	and ebx, 1
	xor ebx, 1						; invert, so 0 is only one entry, 1 so we can use upper 16bits

	lea rax, [r13 + rax * 2]		; VRAM Address + position * 2

	and rax, 01ffffh
	mov eax, dword ptr[rsi + rax]	; now has tile number(ah) and data(al)
	ret
endm

layer_get_map_proc macro _map_height, _map_width, _tile_height, _tile_width, _tile_map_count
layer_get_map_&_tile_map_count& proc
	get_map_data _map_height, _map_width, _tile_height, _tile_width
layer_get_map_&_tile_map_count& endp
endm

layer_get_map_proc 0, 0, 0, 0, 0
layer_get_map_proc 0, 0, 0, 1, 1
layer_get_map_proc 0, 0, 1, 0, 2
layer_get_map_proc 0, 0, 1, 1, 3
layer_get_map_proc 0, 1, 0, 0, 4
layer_get_map_proc 0, 1, 0, 1, 5
layer_get_map_proc 0, 1, 1, 0, 6
layer_get_map_proc 0, 1, 1, 1, 7
layer_get_map_proc 0, 2, 0, 0, 8
layer_get_map_proc 0, 2, 0, 1, 9
layer_get_map_proc 0, 2, 1, 0, 10
layer_get_map_proc 0, 2, 1, 1, 11
layer_get_map_proc 0, 3, 0, 0, 12
layer_get_map_proc 0, 3, 0, 1, 13
layer_get_map_proc 0, 3, 1, 0, 14
layer_get_map_proc 0, 3, 1, 1, 15
layer_get_map_proc 1, 0, 0, 0, 16
layer_get_map_proc 1, 0, 0, 1, 17
layer_get_map_proc 1, 0, 1, 0, 18
layer_get_map_proc 1, 0, 1, 1, 19
layer_get_map_proc 1, 1, 0, 0, 20
layer_get_map_proc 1, 1, 0, 1, 21
layer_get_map_proc 1, 1, 1, 0, 22
layer_get_map_proc 1, 1, 1, 1, 23
layer_get_map_proc 1, 2, 0, 0, 24
layer_get_map_proc 1, 2, 0, 1, 25
layer_get_map_proc 1, 2, 1, 0, 26
layer_get_map_proc 1, 2, 1, 1, 27
layer_get_map_proc 1, 3, 0, 0, 28
layer_get_map_proc 1, 3, 0, 1, 29
layer_get_map_proc 1, 3, 1, 0, 30
layer_get_map_proc 1, 3, 1, 1, 31
layer_get_map_proc 2, 0, 0, 0, 32
layer_get_map_proc 2, 0, 0, 1, 33
layer_get_map_proc 2, 0, 1, 0, 34
layer_get_map_proc 2, 0, 1, 1, 35
layer_get_map_proc 2, 1, 0, 0, 36
layer_get_map_proc 2, 1, 0, 1, 37
layer_get_map_proc 2, 1, 1, 0, 38
layer_get_map_proc 2, 1, 1, 1, 39
layer_get_map_proc 2, 2, 0, 0, 40
layer_get_map_proc 2, 2, 0, 1, 41
layer_get_map_proc 2, 2, 1, 0, 42
layer_get_map_proc 2, 2, 1, 1, 43
layer_get_map_proc 2, 3, 0, 0, 44
layer_get_map_proc 2, 3, 0, 1, 45
layer_get_map_proc 2, 3, 1, 0, 46
layer_get_map_proc 2, 3, 1, 1, 47
layer_get_map_proc 3, 0, 0, 0, 48
layer_get_map_proc 3, 0, 0, 1, 49
layer_get_map_proc 3, 0, 1, 0, 50
layer_get_map_proc 3, 0, 1, 1, 51
layer_get_map_proc 3, 1, 0, 0, 52
layer_get_map_proc 3, 1, 0, 1, 53
layer_get_map_proc 3, 1, 1, 0, 54
layer_get_map_proc 3, 1, 1, 1, 55
layer_get_map_proc 3, 2, 0, 0, 56
layer_get_map_proc 3, 2, 0, 1, 57
layer_get_map_proc 3, 2, 1, 0, 58
layer_get_map_proc 3, 2, 1, 1, 59
layer_get_map_proc 3, 3, 0, 0, 60
layer_get_map_proc 3, 3, 0, 1, 61
layer_get_map_proc 3, 3, 1, 0, 62
layer_get_map_proc 3, 3, 1, 1, 63

align 8
get_map_jump:
qword layer_get_map_0 - get_map_jump
qword layer_get_map_1 - get_map_jump
qword layer_get_map_2 - get_map_jump
qword layer_get_map_3 - get_map_jump
qword layer_get_map_4 - get_map_jump
qword layer_get_map_5 - get_map_jump
qword layer_get_map_6 - get_map_jump
qword layer_get_map_7 - get_map_jump
qword layer_get_map_8 - get_map_jump
qword layer_get_map_9 - get_map_jump
qword layer_get_map_10 - get_map_jump
qword layer_get_map_11 - get_map_jump
qword layer_get_map_12 - get_map_jump
qword layer_get_map_13 - get_map_jump
qword layer_get_map_14 - get_map_jump
qword layer_get_map_15 - get_map_jump
qword layer_get_map_16 - get_map_jump
qword layer_get_map_17 - get_map_jump
qword layer_get_map_18 - get_map_jump
qword layer_get_map_19 - get_map_jump
qword layer_get_map_20 - get_map_jump
qword layer_get_map_21 - get_map_jump
qword layer_get_map_22 - get_map_jump
qword layer_get_map_23 - get_map_jump
qword layer_get_map_24 - get_map_jump
qword layer_get_map_25 - get_map_jump
qword layer_get_map_26 - get_map_jump
qword layer_get_map_27 - get_map_jump
qword layer_get_map_28 - get_map_jump
qword layer_get_map_29 - get_map_jump
qword layer_get_map_30 - get_map_jump
qword layer_get_map_31 - get_map_jump
qword layer_get_map_32 - get_map_jump
qword layer_get_map_33 - get_map_jump
qword layer_get_map_34 - get_map_jump
qword layer_get_map_35 - get_map_jump
qword layer_get_map_36 - get_map_jump
qword layer_get_map_37 - get_map_jump
qword layer_get_map_38 - get_map_jump
qword layer_get_map_39 - get_map_jump
qword layer_get_map_40 - get_map_jump
qword layer_get_map_41 - get_map_jump
qword layer_get_map_42 - get_map_jump
qword layer_get_map_43 - get_map_jump
qword layer_get_map_44 - get_map_jump
qword layer_get_map_45 - get_map_jump
qword layer_get_map_46 - get_map_jump
qword layer_get_map_47 - get_map_jump
qword layer_get_map_48 - get_map_jump
qword layer_get_map_49 - get_map_jump
qword layer_get_map_50 - get_map_jump
qword layer_get_map_51 - get_map_jump
qword layer_get_map_52 - get_map_jump
qword layer_get_map_53 - get_map_jump
qword layer_get_map_54 - get_map_jump
qword layer_get_map_55 - get_map_jump
qword layer_get_map_56 - get_map_jump
qword layer_get_map_57 - get_map_jump
qword layer_get_map_58 - get_map_jump
qword layer_get_map_59 - get_map_jump
qword layer_get_map_60 - get_map_jump
qword layer_get_map_61 - get_map_jump
qword layer_get_map_62 - get_map_jump
qword layer_get_map_63 - get_map_jump
