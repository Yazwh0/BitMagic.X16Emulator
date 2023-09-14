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

; macro to find the current bitmap definition, returns data in ax. 
; width 0: 320, 1: 640
; expects:
; r14: current layer tile address
; r11: x
; r12: y
; returns
; ebx : tile data
; r13 : width
; r14 : x mask
get_bitmap_data macro width, colour_depth
	add dword ptr [rdx].state.vram_wait, 1

	mov rsi, [rdx].state.vram_ptr
	
	if width eq 0
		lea r10, bitmap_width_320_lut
	endif
	if width eq 1
		lea r10, bitmap_width_640_lut
	endif

	mov r10, [r10 + r12 * 8]				; y * 320 or 640
	add r10, r11							; add x to get pixel position

	if colour_depth eq 0					; reduce down to offset
		shr r10, 3
	endif
	if colour_depth eq 1
		shr r10, 2
	endif
	if colour_depth eq 2
		shr r10, 1
	endif
	if colour_depth eq 3
	endif
	add r10, r14							; r10 is now in bytes offset, add on base address
	and r10, 1ffffh							; constrain to vram

	mov ebx, [rsi + r10]
	ret
endm

bitmap_data_proc macro _bitmap_width, _colour_depth, _bitmap_definition_count
bitmap_data_&_bitmap_definition_count& proc
	get_bitmap_data _bitmap_width, _colour_depth
bitmap_data_&_bitmap_definition_count& endp
endm

bitmap_data_proc 0, 0, 0
bitmap_data_proc 1, 0, 1
bitmap_data_proc 0, 1, 2
bitmap_data_proc 1, 1, 3
bitmap_data_proc 0, 2, 4
bitmap_data_proc 1, 2, 5
bitmap_data_proc 0, 3, 6
bitmap_data_proc 1, 3, 7

align 8
get_bitmap_jump:
	qword bitmap_data_0 - get_bitmap_jump
	qword bitmap_data_1 - get_bitmap_jump
	qword bitmap_data_2 - get_bitmap_jump
	qword bitmap_data_3 - get_bitmap_jump
	qword bitmap_data_4 - get_bitmap_jump
	qword bitmap_data_5 - get_bitmap_jump
	qword bitmap_data_6 - get_bitmap_jump
	qword bitmap_data_7 - get_bitmap_jump

