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

writepixel_1bpp_bitmap macro pixeloutput, outputoffset
	xor r13, r13
	shr ebx, 1
	cmovc r13, r11

	mov byte ptr [rsi + r15 + pixeloutput + outputoffset], r13b
endm

layer0_1bpp_bmp_render proc
	; ebx contains the pixel data - 32bits worth
	; r15 is the buffer position
	mov rsi, [rdx].state.memory_ptr
	movzx r11, byte ptr [rsi + L0_HSCROLL_H]		; get offset
	shl r11, 4
	add r11, 1

	mov rsi, [rdx].state.display_buffer_ptr

	writepixel_1bpp_bitmap 07, BUFFER_LAYER0	
	writepixel_1bpp_bitmap 06, BUFFER_LAYER0
	writepixel_1bpp_bitmap 05, BUFFER_LAYER0
	writepixel_1bpp_bitmap 04, BUFFER_LAYER0
	writepixel_1bpp_bitmap 03, BUFFER_LAYER0
	writepixel_1bpp_bitmap 02, BUFFER_LAYER0
	writepixel_1bpp_bitmap 01, BUFFER_LAYER0
	writepixel_1bpp_bitmap 00, BUFFER_LAYER0

	writepixel_1bpp_bitmap 15, BUFFER_LAYER0
	writepixel_1bpp_bitmap 14, BUFFER_LAYER0
	writepixel_1bpp_bitmap 13, BUFFER_LAYER0
	writepixel_1bpp_bitmap 12, BUFFER_LAYER0
	writepixel_1bpp_bitmap 11, BUFFER_LAYER0
	writepixel_1bpp_bitmap 10, BUFFER_LAYER0
	writepixel_1bpp_bitmap 09, BUFFER_LAYER0
	writepixel_1bpp_bitmap 08, BUFFER_LAYER0

	writepixel_1bpp_bitmap 23, BUFFER_LAYER0
	writepixel_1bpp_bitmap 22, BUFFER_LAYER0
	writepixel_1bpp_bitmap 21, BUFFER_LAYER0
	writepixel_1bpp_bitmap 20, BUFFER_LAYER0
	writepixel_1bpp_bitmap 19, BUFFER_LAYER0
	writepixel_1bpp_bitmap 18, BUFFER_LAYER0
	writepixel_1bpp_bitmap 17, BUFFER_LAYER0
	writepixel_1bpp_bitmap 16, BUFFER_LAYER0

	writepixel_1bpp_bitmap 31, BUFFER_LAYER0
	writepixel_1bpp_bitmap 30, BUFFER_LAYER0
	writepixel_1bpp_bitmap 29, BUFFER_LAYER0
	writepixel_1bpp_bitmap 28, BUFFER_LAYER0
	writepixel_1bpp_bitmap 27, BUFFER_LAYER0
	writepixel_1bpp_bitmap 26, BUFFER_LAYER0
	writepixel_1bpp_bitmap 25, BUFFER_LAYER0
	writepixel_1bpp_bitmap 24, BUFFER_LAYER0

	mov eax, 32

	ret

layer0_1bpp_bmp_render endp

layer1_1bpp_bmp_render proc
	; ebx contains the pixel data - 32bits worth
	; r15 is the buffer position
	mov rsi, [rdx].state.memory_ptr
	movzx r11, byte ptr [rsi + L1_HSCROLL_H]		; get offset
	shl r11, 4
	add r11, 1

	mov rsi, [rdx].state.display_buffer_ptr

	writepixel_1bpp_bitmap 07, BUFFER_LAYER1	
	writepixel_1bpp_bitmap 06, BUFFER_LAYER1
	writepixel_1bpp_bitmap 05, BUFFER_LAYER1
	writepixel_1bpp_bitmap 04, BUFFER_LAYER1
	writepixel_1bpp_bitmap 03, BUFFER_LAYER1
	writepixel_1bpp_bitmap 02, BUFFER_LAYER1
	writepixel_1bpp_bitmap 01, BUFFER_LAYER1
	writepixel_1bpp_bitmap 00, BUFFER_LAYER1

	writepixel_1bpp_bitmap 15, BUFFER_LAYER1
	writepixel_1bpp_bitmap 14, BUFFER_LAYER1
	writepixel_1bpp_bitmap 13, BUFFER_LAYER1
	writepixel_1bpp_bitmap 12, BUFFER_LAYER1
	writepixel_1bpp_bitmap 11, BUFFER_LAYER1
	writepixel_1bpp_bitmap 10, BUFFER_LAYER1
	writepixel_1bpp_bitmap 09, BUFFER_LAYER1
	writepixel_1bpp_bitmap 08, BUFFER_LAYER1

	writepixel_1bpp_bitmap 23, BUFFER_LAYER1
	writepixel_1bpp_bitmap 22, BUFFER_LAYER1
	writepixel_1bpp_bitmap 21, BUFFER_LAYER1
	writepixel_1bpp_bitmap 20, BUFFER_LAYER1
	writepixel_1bpp_bitmap 19, BUFFER_LAYER1
	writepixel_1bpp_bitmap 18, BUFFER_LAYER1
	writepixel_1bpp_bitmap 17, BUFFER_LAYER1
	writepixel_1bpp_bitmap 16, BUFFER_LAYER1

	writepixel_1bpp_bitmap 31, BUFFER_LAYER1
	writepixel_1bpp_bitmap 30, BUFFER_LAYER1
	writepixel_1bpp_bitmap 29, BUFFER_LAYER1
	writepixel_1bpp_bitmap 28, BUFFER_LAYER1
	writepixel_1bpp_bitmap 27, BUFFER_LAYER1
	writepixel_1bpp_bitmap 26, BUFFER_LAYER1
	writepixel_1bpp_bitmap 25, BUFFER_LAYER1
	writepixel_1bpp_bitmap 24, BUFFER_LAYER1

	mov eax, 32

	ret

layer1_1bpp_bmp_render endp