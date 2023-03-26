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

writepixel_4bpp_bitmap macro pixeloutput, outputoffset
	xor r12, r12
	mov r13, rbx
	and r13, 0fh		; mask off colour
	cmovnz r12, r11		; clear offset if not zero
	add r13, r12		; add offset

	mov byte ptr [rsi + r15 + pixeloutput + outputoffset], r13b
	shr ebx, 4
endm

layer0_4bpp_bmp_render proc
	; ebx contains the pixel data - 32bits worth
	; r15 is the buffer position
	mov rsi, [rdx].state.memory_ptr
	movzx r11, byte ptr [rsi + L0_HSCROLL_H]		; get offset
	shl r11, 4

	mov rsi, [rdx].state.display_buffer_ptr

	writepixel_4bpp_bitmap 01, BUFFER_LAYER0
	writepixel_4bpp_bitmap 00, BUFFER_LAYER0

	writepixel_4bpp_bitmap 03, BUFFER_LAYER0
	writepixel_4bpp_bitmap 02, BUFFER_LAYER0

	writepixel_4bpp_bitmap 05, BUFFER_LAYER0
	writepixel_4bpp_bitmap 04, BUFFER_LAYER0

	writepixel_4bpp_bitmap 07, BUFFER_LAYER0
	writepixel_4bpp_bitmap 06, BUFFER_LAYER0

	mov rax, 8

	jmp layer0_render_done

layer0_4bpp_bmp_render endp

layer1_4bpp_bmp_render proc
	; ebx contains the pixel data - 32bits worth
	; r15 is the buffer position
	mov rsi, [rdx].state.memory_ptr
	movzx r11, byte ptr [rsi + L1_HSCROLL_H]		; get offset
	shl r11, 4

	mov rsi, [rdx].state.display_buffer_ptr

	writepixel_4bpp_bitmap 01, BUFFER_LAYER1
	writepixel_4bpp_bitmap 00, BUFFER_LAYER1

	writepixel_4bpp_bitmap 03, BUFFER_LAYER1
	writepixel_4bpp_bitmap 02, BUFFER_LAYER1

	writepixel_4bpp_bitmap 05, BUFFER_LAYER1
	writepixel_4bpp_bitmap 04, BUFFER_LAYER1

	writepixel_4bpp_bitmap 07, BUFFER_LAYER1
	writepixel_4bpp_bitmap 06, BUFFER_LAYER1

	mov rax, 8

	jmp layer1_render_done

layer1_4bpp_bmp_render endp