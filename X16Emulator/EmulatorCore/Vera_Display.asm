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
include State.asm

; rax  : scratch
; rbx  : scratch
; rcx  : loop target
; rdx  : state object 
; rsi  : memory context (changable)
; rdi  : display
; r8   : scratch
; r9   : output offset
; r10  : scratch
; r11  : x
; r12  : y
; r13  : scratch
; r14  : scratch
; r15  : buffer render output position for rendering in\out of buffer




SCREEN_WIDTH		equ 800
SCREEN_HEIGHT		equ 525
SCREEN_DOTS			equ SCREEN_WIDTH * SCREEN_HEIGHT
RENDER_RESET		equ SCREEN_DOTS - 800

; Screen is RGBA so * 4.
SCREEN_BUFFER_SIZE	equ SCREEN_DOTS * 4
BACKGROUND			equ 0
SPRITE_L1			equ SCREEN_BUFFER_SIZE
LAYER0				equ SCREEN_BUFFER_SIZE * 2
SPRITE_L2			equ SCREEN_BUFFER_SIZE * 3
LAYER1				equ SCREEN_BUFFER_SIZE * 4
SPRITE_L3			equ SCREEN_BUFFER_SIZE * 5
SPRITE_DEBUG		equ SCREEN_BUFFER_SIZE * 6

VISIBLE_WIDTH		equ 640
VISIBLE_HEIGHT		equ 480

VBLANK				equ 480

; Buffer is colour index. one line being rendered, the other being output
BUFFER_SIZE			equ 2048 * 2			; use 2048, so we can toggle high bit to switch, also needs to be wide enough for scaling of $ff
BUFFER_LAYER0		equ 0
BUFFER_LAYER1		equ BUFFER_SIZE * 1
BUFFER_SPRITE_VALUE	equ BUFFER_SIZE * 2
BUFFER_SPRITE_DEPTH	equ BUFFER_SIZE * 3
BUFFER_SPRITE_COL	equ BUFFER_SIZE * 4
BUFFER_SPRITE_DEBUG equ BUFFER_SIZE * 5

STATE_WAIT_START		equ 0
STATE_FETCH_MAP			equ 1
STATE_FETCH_MAP_WAIT	equ 2
STATE_FETCH_TILE		equ 3
STATE_FETCH_TILE_WAIT	equ 4
STATE_RENDER_TILE		equ 5
STATE_FETCH_BITMAP		equ 6
STATE_FETCH_BITMAP_WAIT equ 7
STATE_RENDER_BITMAP		equ 8

include Vera_Get_Map.asm
include Vera_Get_Tile.asm
include Vera_Get_Bitmap.asm

;
; Render the rest of the display, only gets called on vsync
;
vera_render_display proc
	; update display
	mov byte ptr cl, [rdx].state.headless
	test cl, cl
	jz not_headless
	ret
not_headless:

	push rax
	mov rax, [rdx].state.vera_clock
	mov [rdx].state.vera_clock, r14				; store vera clock
	mov rcx, r14								; Cpu Clock ticks
	sub rcx, rax								; Take off vera ticks for the number of cpu ticks we need to process

	jnz change									; if nothing to do, leave
	pop rax
	ret

change:
	push rsi
	push rdi
	push r8
	push r9
	push r10
	push rbx 
	push r11
	push r12
	push r13
	push r14
	push r15

	lea rcx, [rcx+rcx*4]						; * 5
	lea rcx, [rcx+rcx*4]						; * 5 = *25

	movzx rax, [rdx].state.display_carry		; Get carry, and add it on
	add rcx, rax

	mov rax, rcx								; Store to trim
	and al, 111b
	mov byte ptr [rdx].state.display_carry, al	; save carry
	shr rcx, 3									; /8 (round), rcx now contains the steps

	mov rsi, [rdx].state.display_buffer_ptr
	mov rdi, [rdx].state.display_ptr
	mov r9d, [rdx].state.display_position
	;mov r11w, [rdx].state.display_x
	mov r15d, [rdx].state.buffer_render_position

	; this also gets set at the end of the display loop
	;movzx r12, word ptr [rdx].state.display_y

display_loop:
	;
	; BORDER and VISIBLE CHECK
	;
	; needs actual x, y coordinates

	movzx r12, word ptr [rdx].state.display_y
	movzx r11, word ptr [rdx].state.display_x

	; check if we're in the visible area as a trivial skip
	lea r10, should_display_table
	movzx rax, byte ptr [r10 + r9]

	lea r10, render_procs
	add r10, [r10 + rax * 8]
	jmp r10

align 8
render_procs:
	qword renderstep_nothing - render_procs
	qword renderstep_read_from_buffer - render_procs
	qword renderstep_normal - render_procs
	qword renderstep_all_to_buffer - render_procs
	qword renderstep_sprites_to_buffer - render_procs
	qword renderstep_next_line - render_procs
	qword renderstep_next_frame - render_procs
	qword renderstep_reset_buffer - render_procs

renders_end::
	;
	; VRAM Counter
	;
	xor rbx, rbx
	mov eax, dword ptr [rdx].state.vram_wait
	sub eax, 1
	cmovs rax, rbx
	mov dword ptr [rdx].state.vram_wait, eax

	dec rcx
	jnz display_loop

done:
	;mov rsi, [rdx].state.vram_ptr

	mov dword ptr [rdx].state.display_position, r9d
	;mov word ptr [rdx].state.display_x, r11w
	mov dword ptr [rdx].state.buffer_render_position, r15d

	pop r15
	pop r14
	pop r13
	pop r12
	pop r11
	pop rbx 
	pop r10
	pop r9
	pop r8
	pop rdi
	pop rsi
	pop rax
	ret

vera_render_display endp


vera_initialise_palette proc	
	xor rax, rax
	mov rsi, [rdx].state.palette_ptr
	lea rcx, vera_default_palette

create_argb_palette:
	; need to go from xRBG
	; to xxBBGGRR as its little endian
	mov r9, 0ff000000h
	mov word ptr bx, [rcx + rax * 2]

	; Red
	mov r8, rbx
	and r8, 0f00h
	shr r8, 8
	or r9, r8

	shl r8, 4
	or r9, r8

	; Green
	mov r8, rbx
	and r8, 000fh
	shl r8, 16
	or r9, r8

	shl r8, 4
	or r9, r8

	;Blue
	mov r8, rbx
	and r8, 00f0h
	shl r8, 4
	or r9, r8

	shl r8, 4
	or r9, r8

	mov dword ptr [rsi + rax * 4], r9d

	add rax, 1
	cmp rax, 100h
	jne create_argb_palette

	ret
vera_initialise_palette endp

;
; render steps
;
renderstep_nothing proc
	add r9, 1
	jmp renders_end
renderstep_nothing endp

renderstep_read_from_buffer proc
	call render_from_buffer

	add r15, 1

	inc word ptr [rdx].state.display_x
	;mov rax, r15
	;and rax, 0011111111111b	; dont consider the top bit
	;mov word ptr [rdx].state.display_x, ax

	add r9, 1
	jmp renders_end
renderstep_read_from_buffer endp

renderstep_normal proc
	call render_layers_to_buffer	
	call render_sprites_to_buffer

	call render_from_buffer

	add r15, 1

	inc word ptr [rdx].state.display_x
	;mov rax, r15
	;and rax, 0011111111111b	; dont consider the top bit
	;mov word ptr [rdx].state.display_x, ax

	add r9, 1
	jmp renders_end
renderstep_normal endp

renderstep_all_to_buffer proc
	call render_layers_to_buffer	
	call render_sprites_to_buffer

	; add x on, normally done in render_from_buffer
	mov r11d, dword ptr [rdx].state.scale_x
	mov eax, [rdx].state.dc_hscale
	add r11, rax
	mov dword ptr [rdx].state.scale_x, r11d

	add r15, 1

	inc word ptr [rdx].state.display_x
	;mov rax, r15
	;and rax, 0011111111111b	; dont consider the top bit
	;mov word ptr [rdx].state.display_x, ax
	
	add r9, 1
	jmp renders_end
renderstep_all_to_buffer endp

renderstep_sprites_to_buffer proc
	call render_sprites_to_buffer


	add r9, 1
	jmp renders_end
renderstep_sprites_to_buffer endp

renderstep_next_line proc	
	; Add 1 to act y
	movzx r12, word ptr [rdx].state.display_y
	add r12, 1
	mov word ptr [rdx].state.display_y, r12w

	; next line, reset counters
	xor r15, 0100000000000b	; flip top bit
	and r15, 0100000000000b	; and clear count
	
	xor r11, r11
	mov word ptr [rdx].state.display_x, r11w	; zero
	mov dword ptr [rdx].state.layer0_x, r11d
	mov dword ptr [rdx].state.layer1_x, r11d
	mov dword ptr [rdx].state.layer0_tilecount, r11d
	mov dword ptr [rdx].state.layer1_tilecount, r11d
	mov dword ptr [rdx].state.layer0_tiledone, -1
	mov dword ptr [rdx].state.layer1_tiledone, -1

	; scale_x needs to have the high bit set as it is used to read out of the buffer
	mov r11, r15
	xor r11, 0100000000000b ; flip top bit back
	shl r11, 16				; adjust to the fixed point number
	mov dword ptr [rdx].state.scale_x, r11d

	; check for vstart
	movzx rax, word ptr [rdx].state.dc_vstart
	cmp rax, r12
	jge no_y_inc_vstart

	; Add line to scaled y
	mov r12d, dword ptr [rdx].state.scale_y
	mov eax, [rdx].state.dc_vscale
	add r12, rax
	mov [rdx].state.scale_y, r12d		

no_y_inc_vstart:
	; reset sprite renderer
	xor rax, rax										; SPRITE_SEARCHING is zero
	mov dword ptr [rdx].state.sprite_render_mode, eax	; start searching
	mov dword ptr [rdx].state.sprite_position, -1		; from sprite 0, but 1 gets added straight away, so start at index -1
	mov dword ptr [rdx].state.sprite_width, eax
	mov dword ptr [rdx].state.sprite_wait, eax
	mov dword ptr [rdx].state.vram_wait, eax
	mov word ptr [rdx].state.display_x, ax
	; clear sprite buffer
	
	call clear_sprite_buffer

	add r9, 1

	; set correct mode for layer
	mov r13d, STATE_FETCH_MAP
	mov r14d, STATE_FETCH_BITMAP

	movzx rax, byte ptr [rdx].state.layer0_bitmapMode
	test rax, rax
	cmovnz r13d, r14d
	mov [rdx].state.layer0_state, r13d

	mov r13d, STATE_FETCH_MAP
	movzx rax, byte ptr [rdx].state.layer1_bitmapMode
	test rax, rax
	cmovnz r13d, r14d
	mov [rdx].state.layer1_state, r13d

	jmp renders_end
	
renderstep_next_line endp


; reset display x and display y.
; add to scaled y as we're already rendering to the buffer
; reset scaled x as we're at the end of a line
renderstep_next_frame proc
	xor r12, r12
	mov word ptr [rdx].state.display_y, r12w

	xor r11, r11
	mov word ptr [rdx].state.display_x, r11w	; zero
	mov dword ptr [rdx].state.layer0_x, r11d
	mov dword ptr [rdx].state.layer1_x, r11d
	mov dword ptr [rdx].state.layer0_tilecount, r11d
	mov dword ptr [rdx].state.layer1_tilecount, r11d
	mov dword ptr [rdx].state.layer0_tiledone, -1
	mov dword ptr [rdx].state.layer1_tiledone, -1

	; Add line to scaled y
	mov r12d, dword ptr [rdx].state.scale_y
	mov eax, [rdx].state.dc_vscale
	add r12, rax
	mov [rdx].state.scale_y, r12d		

	; next line, reset counters
	xor r15, 0100000000000b	; flip top bit
	and r15, 0100000000000b	; and clear count

	; scale_x needs to have the high bit set as it is used to read out of the buffer
	mov r11, r15
	xor r11, 0100000000000b ; flip top bit back
	shl r11, 16				; adjust to the fixed point number
	mov dword ptr [rdx].state.scale_x, r11d

	; reset sprite renderer
	xor rax, rax										; SPRITE_SEARCHING is zero
	mov dword ptr [rdx].state.sprite_render_mode, eax	; start searching
	mov dword ptr [rdx].state.sprite_position, -1		; from sprite 0, but 1 gets added straight away, so start at index -1
	mov dword ptr [rdx].state.sprite_width, eax
	mov dword ptr [rdx].state.sprite_wait, eax
	mov dword ptr [rdx].state.vram_wait, eax
	
	mov r13d, STATE_FETCH_MAP
	mov r14d, STATE_FETCH_BITMAP

	movzx rax, byte ptr [rdx].state.layer0_bitmapMode
	test rax, rax
	cmovnz r13d, r14d
	mov [rdx].state.layer0_state, r13d

	mov r13d, STATE_FETCH_MAP
	movzx rax, byte ptr [rdx].state.layer1_bitmapMode
	test rax, rax
	cmovnz r13d, r14d
	mov [rdx].state.layer1_state, r13d

	; clear sprite buffer

	call clear_sprite_buffer

	xor r9, r9

	jmp renders_end
renderstep_next_frame endp

; reset scale_y as this is whats used to write into the buffer
renderstep_reset_buffer proc
	add r9, 1

	; Add 1 to act y
	movzx r12, word ptr [rdx].state.display_y
	add r12, 1
	mov word ptr [rdx].state.display_y, r12w


	xor r11, r11
	mov word ptr [rdx].state.display_x, r11w	; zero
	mov dword ptr [rdx].state.layer0_x, r11d
	mov dword ptr [rdx].state.layer1_x, r11d
	mov dword ptr [rdx].state.layer0_tilecount, r11d
	mov dword ptr [rdx].state.layer1_tilecount, r11d
	mov dword ptr [rdx].state.layer0_tiledone, -1
	mov dword ptr [rdx].state.layer1_tiledone, -1

	xor r12, r12
	mov dword ptr [rdx].state.scale_y, r12d				; reset scaled value 
	;xor r15, r15										; reset buffer pointer
;	mov word ptr [rdx].state.layer0_next_render, 1		; next pixel forces a draw
;	mov word ptr [rdx].state.layer1_next_render, 1

	
	xor r15, 0100000000000b	; flip top bit
	and r15, 0100000000000b	; and clear count

	; scale_x needs to have the high bit set as it is used to read out of the buffer
	mov r11, r15
	xor r11, 0100000000000b ; flip top bit back
	shl r11, 16				; adjust to the fixed point number
	mov dword ptr [rdx].state.scale_x, r11d

	; reset sprite renderer
	xor rax, rax										; SPRITE_SEARCHING is zero
	mov dword ptr [rdx].state.sprite_render_mode, eax	; start searching
	mov dword ptr [rdx].state.sprite_position, -1		; from sprite 0, but 1 gets added straight away, so start at index -1
	mov dword ptr [rdx].state.sprite_width, eax
	mov dword ptr [rdx].state.sprite_wait, eax
	mov dword ptr [rdx].state.vram_wait, eax
	
	mov r13d, STATE_FETCH_MAP
	mov r14d, STATE_FETCH_BITMAP

	movzx rax, byte ptr [rdx].state.layer0_bitmapMode
	test rax, rax
	cmovnz r13d, r14d
	mov [rdx].state.layer0_state, r13d

	mov r13d, STATE_FETCH_MAP
	movzx rax, byte ptr [rdx].state.layer1_bitmapMode
	test rax, rax
	cmovnz r13d, r14d
	mov [rdx].state.layer1_state, r13d

	call clear_sprite_buffer

	jmp renders_end
renderstep_reset_buffer endp


render_from_buffer proc
	movzx r12, word ptr [rdx].state.display_y
	movzx r11, word ptr [rdx].state.display_x

	movzx rax, word ptr [rdx].state.dc_vstart
	cmp r12, rax
	jl draw_border

	mov ax, word ptr [rdx].state.dc_vstop
	cmp r12w, ax
	jge draw_border

	mov ax, word ptr [rdx].state.dc_hstart
	cmp r11w, ax
	jl draw_border

	mov ax, word ptr [rdx].state.dc_hstop
	cmp r11w, ax
	jl draw_pixel

draw_border:
	mov r8, [rdx].state.palette_ptr
	movzx rax, [rdx].state.dc_border
	mov ebx, dword ptr [r8 + rax * 4]
	mov [rdi + r9 * 4 + BACKGROUND], ebx
	xor ebx, ebx
	mov [rdi + r9 * 4 + LAYER0], ebx
	mov [rdi + r9 * 4 + LAYER1], ebx

	jmp draw_end

;
; VIDEO Output
;
; Needs scaled x, buffer position (r15)
;
draw_pixel:
	mov r8, [rdx].state.palette_ptr

	mov r11d, dword ptr [rdx].state.scale_x ; should have high bit set for indexing into the buffer
	shr r11, 16

	mov rsi, [rdx].state.display_buffer_ptr

	; background
	mov ebx, dword ptr [r8]
	mov [rdi + r9 * 4 + BACKGROUND], ebx

	;
	; layer 0
	;
	mov al, byte ptr [rdx].state.layer0_enable
	test al, al
	jz layer0_notenabled

	movzx rax, byte ptr [rsi + r11 + BUFFER_LAYER0]			; read the colour index from the buffer
	xor rbx, rbx
	test rax, rax
	cmovnz ebx, dword ptr [r8 + rax * 4]		; get colour from palette
	mov [rdi + r9 * 4 + LAYER0], ebx
	jmp layer0_done

layer0_notenabled:
	;xor rbx, rbx								; if layer is not enabled, write a transparent pixel
	mov dword ptr [rdi + r9 * 4 + LAYER0], 0	
layer0_done:

	;
	; layer 1
	;
	mov al, byte ptr [rdx].state.layer1_enable
	test al, al
	jz layer1_notenabled

	movzx rax, byte ptr [rsi + r11 + BUFFER_LAYER1]			; read the colour index from the buffer
	xor rbx, rbx
	test rax, rax
	cmovnz ebx, dword ptr [r8 + rax * 4]
	mov [rdi + r9 * 4 + LAYER1], ebx
	jmp layer1_done

layer1_notenabled:
	;xor rbx, rbx								; if layer is not enabled, write a transparent pixel
	mov dword ptr [rdi + r9 * 4 + LAYER1], 0
layer1_done:
	;
	; Sprites
	;

	mov al, byte ptr [rdx].state.sprite_enable
	test al, al
	jz sprites_not_enabled

	;push r14
	;push r13

	movzx rax, byte ptr [rsi + r11 + BUFFER_SPRITE_VALUE]	; read the colour index from the buffer
	movzx r14, byte ptr [rsi + r11 + BUFFER_SPRITE_DEPTH]	; will be 0, 1, 2 or 3. If zero then should be no pixel.

	xor rbx, rbx
	test rax, rax
	cmovnz ebx, dword ptr [r8 + rax * 4]					; gets actual colour for the pixel -- RGBA

	movzx r11d, byte ptr [rsi + r11 + BUFFER_SPRITE_DEBUG]	
	mov rax, [rdx].state.debug_sprites_colours_ptr
	mov r11d, dword ptr [rax + r11 * 4]
;	test r11d, r11d											; sprite present?
;	mov r11d, 0500000ffh									; transparaent red
;	cmovz r11d, eax											; clear debug colour if no value

	xor rax, rax

	lea r13, depth_jump_table
	add r13, [r13 + r14 * 8]
	jmp r13

align 8
depth_jump_table:
	dq depth_0 - depth_jump_table
	dq depth_1 - depth_jump_table
	dq depth_2 - depth_jump_table
	dq depth_3 - depth_jump_table

depth_0:
sprites_not_enabled:										; rax will be zero, so no write
	mov dword ptr [rdi + r9 * 4 + SPRITE_L1], eax
	mov dword ptr [rdi + r9 * 4 + SPRITE_L2], eax
	mov dword ptr [rdi + r9 * 4 + SPRITE_L3], eax
	mov dword ptr [rdi + r9 * 4 + SPRITE_DEBUG], r11d
	jmp sprite_render_done
depth_1:
	mov dword ptr [rdi + r9 * 4 + SPRITE_L1], ebx
	mov dword ptr [rdi + r9 * 4 + SPRITE_L2], eax
	mov dword ptr [rdi + r9 * 4 + SPRITE_L3], eax
	mov dword ptr [rdi + r9 * 4 + SPRITE_DEBUG], r11d
	jmp sprite_render_done
depth_2:
	mov dword ptr [rdi + r9 * 4 + SPRITE_L1], eax
	mov dword ptr [rdi + r9 * 4 + SPRITE_L2], ebx
	mov dword ptr [rdi + r9 * 4 + SPRITE_L3], eax
	mov dword ptr [rdi + r9 * 4 + SPRITE_DEBUG], r11d
	jmp sprite_render_done
depth_3:
	mov dword ptr [rdi + r9 * 4 + SPRITE_L1], eax
	mov dword ptr [rdi + r9 * 4 + SPRITE_L2], eax
	mov dword ptr [rdi + r9 * 4 + SPRITE_L3], ebx
	mov dword ptr [rdi + r9 * 4 + SPRITE_DEBUG], r11d

sprite_render_done:
	;pop r13
	;pop r14


	;
	; All done
	;
	
	; step x on, and store
	mov r11d, dword ptr [rdx].state.scale_x
	mov eax, [rdx].state.dc_hscale
	add r11, rax
	mov dword ptr [rdx].state.scale_x, r11d

;
;	Display complete.
;
draw_end:
	ret
render_from_buffer endp


render_sprites_to_buffer proc
	;
	; Sprites
	;
	mov eax, dword ptr [rdx].state.sprite_wait
	test rax, rax
	jnz sprite_waiting

	push rdi
	push r12
	push r15
		; set r12 to scaled y 
	mov r12d, dword ptr [rdx].state.scale_y
	shr r12, 16							; adjust to actual value


	;add r12w, word ptr [rdx].state.layer1_vscroll
	and r15, 0100000000000b			; set r15\buffer position to be just the offset
	call sprites_render	
	pop r15
	pop r12
	pop rdi

	ret

sprite_waiting:
	sub rax, 1
	mov dword ptr [rdx].state.sprite_wait, eax

	ret
render_sprites_to_buffer endp

;
; Renderers
;

include Vera_Sprites.asm
include Vera_Display_Tiles_1bpp.asm
include Vera_Display_Tiles_2bpp.asm
include Vera_Display_Tiles_4bpp.asm
include Vera_Display_Tiles_8bpp.asm
include Vera_Display_Bitmap_1bpp.asm
include Vera_Display_Bitmap_2bpp.asm
include Vera_Display_Bitmap_4bpp.asm
include Vera_Display_Bitmap_8bpp.asm

; macro to find the current bitmap definition, returns data in bx. 
; width 0: 320, 1: 640
; expects:
; r14: current layer tile address
; r11: x
; r12: y
; returns
; ebx : tile data
; r13 : width
; r14 : x mask
get_bitmap_definition macro width, colour_depth
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

; macro to find the current tile definition, returns data in ax. 
; updates:
; rax: tile information (TODO!!!)
; rbx: tile location (if -1 then perform location search)
; r13: current layer map address
; r14: current layer tile address
; r11: x
; r12: y
; returns
; ax  : tile information
; ebx : tile data
; r10 : x position through tile
; r13 : width
; r14 : x mask
; r8 : tile location to be stored and passed back in rbx

get_tile_definition macro map_height, map_width, tile_height, tile_width, colour_depth
	local m_height_px, m_width_px, t_height_px, t_width_px, t_colour_size, t_size_shift, t_tile_mask, t_multiplier, t_colour_mask, t_tile_shift, t_tile_x_mask, t_height_invert_mask, position_found, has_data, test_bypass

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

	cmp rbx, -1
	jne has_data

	add dword ptr [rdx].state.vram_wait, 1	; for tile map read

	mov rax, r12					; y
	shr rax, tile_height + 3		; / tile height
	and rax, m_height_px - 1		; constrain to map
	shl rax, map_width + 5			; * map width

	;xor rbx, rbx
	mov rbx, r11					; x
	shr rbx, tile_width + 3			; / tile width
	and rbx, m_width_px - 1			; constrain to map
	add rax, rbx	

	;and rax, (m_height_px * m_width_px) - 1

	lea rax, [r13 + rax * 2]		; VRAM Address + position * 2

	movzx eax, word ptr [rsi + rax]	; now has tile number (ah) and data (al)

	xor rbx, rbx

	if colour_depth eq 00
		mov bl, al								; get tile number
	else
		mov bx, ax
		and bx, 01111111111b					; mask off tile index
	endif

	; r12 is y, need to convert it to where the line starts in memory
	if colour_depth ne 0
		bt eax, 11							; check if flipped
		jnc no_v_flip

		xor r12, t_height_invert_mask			; inverts the y position

		no_v_flip:
	endif
	and r12, t_tile_mask					; mask for tile height, so now line within tile
	shl r12, t_tile_shift					; adjust to width of line to get offset address

	shl rbx, t_size_shift					; rbx is now the address
	or rbx, r12								; adjust to the line offset
	add rbx, r14							; add to tile base address

	;jmp position_found
has_data:
	; inject rbx here


position_found:
	;mov r8, rbx								; store position for later so it can be saved if necessary
	add dword ptr [rdx].state.vram_wait, 1	; for data read

	mov r8, -1									; no more vram reads for this tile -- gets overwritten later if necessary

	; find dword in memory that is being rendered

	; check if we're part way through a tile, if so store the value for the next render (so vram access is counted) 
	if tile_width eq 1 and colour_depth eq 2
		mov r14, r11
		and r14, 01000b
		cmovz r8, rbx
	endif
	if tile_width eq 0 and colour_depth eq 3
		mov r14, r11
		and r14, 0111b
		cmp r14, 0111b - 3
		cmovl r8, rbx
	endif
	if tile_width eq 1 and colour_depth eq 3
		mov r14, r11
		and r14, 01111b
		cmp r14, 01111b - 3
		cmovl r8, rbx
	endif

	; find offset of current x so the render can start from the right position
	mov r10, r11							
	mov r14, t_tile_x_mask						; r14 is a returned value
	and r10, r14								; return pixels (offset from boundary)

	if colour_depth ne 0 and colour_depth ne 1
		if tile_width eq 1 or colour_depth eq 2 or colour_depth eq 3
			bt eax, 10
			jnc no_h_flip

			if tile_width eq 1 and colour_depth eq 2
				xor r11, 01000b							; flip bit to invert, its masked later
			endif
			if tile_width eq 0 and colour_depth eq 3
				xor r11, 01100b							; flip bit to invert, its masked later
			endif
			if tile_width eq 1 and colour_depth eq 3
				xor r11, 011100b						; flip bit to invert, its masked later
			endif

		no_h_flip:
		endif
	endif

	if tile_width eq 1 and colour_depth eq 2	; 4bpp
		and r11, 01000b							; mask x position
		shr r11, 1								; /2 (ratio for 4bpp to memory) adjust to the actual address
		add rbx, r11
	endif

	if tile_width eq 0 and colour_depth eq 3
		and r11, 01100b							; mask x position
		add rbx, r11
	endif

	if tile_width eq 1 and colour_depth eq 3
		and r11, 01100b						; mask x position
		add rbx, r11
	endif

	mov ebx, dword ptr [rsi + rbx]				; set ebx 32bits worth of values

	if colour_depth eq 0 or colour_depth eq 1
		mov r13, t_width_px						; return width -- not needed for 4 or 8 bpp
	endif

	; now rbx has 32bit from tile data location
	ret
endm

layer0_render_jump:
	layer0_1bpp_til_x qword layer0_1bpp_til_x_render - layer0_render_jump
	layer0_2bpp_til_x qword layer0_2bpp_til_x_render - layer0_render_jump
	layer0_4bpp_til_x qword layer0_4bpp_til_x_render - layer0_render_jump
	layer0_8bpp_til_x qword layer0_8bpp_til_x_render - layer0_render_jump
	layer0_1bpp_bit_x qword layer0_1bpp_bmp_render - layer0_render_jump
	layer0_2bpp_bit_x qword layer0_2bpp_bmp_render - layer0_render_jump
	layer0_4bpp_bit_x qword layer0_4bpp_bmp_render - layer0_render_jump
	layer0_8bpp_bit_x qword layer0_8bpp_bmp_render - layer0_render_jump
	layer0_1bpp_til_t qword layer0_1bpp_til_t_render - layer0_render_jump
	layer0_2bpp_til_t qword layer0_2bpp_til_x_render - layer0_render_jump
	layer0_4bpp_til_t qword layer0_4bpp_til_x_render - layer0_render_jump
	layer0_8bpp_til_t qword layer0_8bpp_til_x_render - layer0_render_jump
	layer0_1bpp_bit_t qword layer0_1bpp_bmp_render - layer0_render_jump
	layer0_2bpp_bit_t qword layer0_2bpp_bmp_render - layer0_render_jump
	layer0_4bpp_bit_t qword layer0_4bpp_bmp_render - layer0_render_jump
	layer0_8bpp_bit_t qword layer0_8bpp_bmp_render - layer0_render_jump

layer1_render_jump:
	layer1_1bpp_til_x qword layer1_1bpp_til_x_render - layer1_render_jump
	layer1_2bpp_til_x qword layer1_2bpp_til_x_render - layer1_render_jump
	layer1_4bpp_til_x qword layer1_4bpp_til_x_render - layer1_render_jump
	layer1_8bpp_til_x qword layer1_8bpp_til_x_render - layer1_render_jump
	layer1_1bpp_bit_x qword layer1_1bpp_bmp_render - layer1_render_jump
	layer1_2bpp_bit_x qword layer1_2bpp_bmp_render - layer1_render_jump
	layer1_4bpp_bit_x qword layer1_4bpp_bmp_render - layer1_render_jump
	layer1_8bpp_bit_x qword layer1_8bpp_bmp_render - layer1_render_jump
	layer1_1bpp_til_t qword layer1_1bpp_til_t_render - layer1_render_jump
	layer1_2bpp_til_t qword layer1_2bpp_til_x_render - layer1_render_jump
	layer1_4bpp_til_t qword layer1_4bpp_til_x_render - layer1_render_jump
	layer1_8bpp_til_t qword layer1_8bpp_til_x_render - layer1_render_jump
	layer1_1bpp_bit_t qword layer1_1bpp_bmp_render - layer1_render_jump
	layer1_2bpp_bit_t qword layer1_2bpp_bmp_render - layer1_render_jump
	layer1_4bpp_bit_t qword layer1_4bpp_bmp_render - layer1_render_jump
	layer1_8bpp_bit_t qword layer1_8bpp_bmp_render - layer1_render_jump


clear_sprite_buffer proc

	vpxor ymm0, ymm0, ymm0	; clears ymm0
	mov rsi, [rdx].state.display_buffer_ptr

	; 01000-0000-0000b
	bt r15, 11
	jc high_part

	lea rsi, [rsi + BUFFER_SPRITE_VALUE]
	jmp clear

high_part:
	lea rsi, [rsi + BUFFER_SPRITE_VALUE+ BUFFER_SIZE/2]

clear:
	vmovdqa ymmword ptr [rsi + 000h], ymm0
	vmovdqa ymmword ptr [rsi + 020h], ymm0
	vmovdqa ymmword ptr [rsi + 040h], ymm0
	vmovdqa ymmword ptr [rsi + 060h], ymm0
	vmovdqa ymmword ptr [rsi + 080h], ymm0
	vmovdqa ymmword ptr [rsi + 0a0h], ymm0
	vmovdqa ymmword ptr [rsi + 0c0h], ymm0
	vmovdqa ymmword ptr [rsi + 0e0h], ymm0
	vmovdqa ymmword ptr [rsi + 100h], ymm0
	vmovdqa ymmword ptr [rsi + 120h], ymm0

	vmovdqa ymmword ptr [rsi + 140h], ymm0
	vmovdqa ymmword ptr [rsi + 160h], ymm0
	vmovdqa ymmword ptr [rsi + 180h], ymm0
	vmovdqa ymmword ptr [rsi + 1a0h], ymm0
	vmovdqa ymmword ptr [rsi + 1c0h], ymm0
	vmovdqa ymmword ptr [rsi + 1e0h], ymm0
	vmovdqa ymmword ptr [rsi + 200h], ymm0
	vmovdqa ymmword ptr [rsi + 220h], ymm0
	vmovdqa ymmword ptr [rsi + 240h], ymm0
	vmovdqa ymmword ptr [rsi + 260h], ymm0

	add rsi, BUFFER_SIZE

	vmovdqa ymmword ptr [rsi + 000h], ymm0
	vmovdqa ymmword ptr [rsi + 020h], ymm0
	vmovdqa ymmword ptr [rsi + 040h], ymm0
	vmovdqa ymmword ptr [rsi + 060h], ymm0
	vmovdqa ymmword ptr [rsi + 080h], ymm0
	vmovdqa ymmword ptr [rsi + 0a0h], ymm0
	vmovdqa ymmword ptr [rsi + 0c0h], ymm0
	vmovdqa ymmword ptr [rsi + 0e0h], ymm0
	vmovdqa ymmword ptr [rsi + 100h], ymm0
	vmovdqa ymmword ptr [rsi + 120h], ymm0

	vmovdqa ymmword ptr [rsi + 140h], ymm0
	vmovdqa ymmword ptr [rsi + 160h], ymm0
	vmovdqa ymmword ptr [rsi + 180h], ymm0
	vmovdqa ymmword ptr [rsi + 1a0h], ymm0
	vmovdqa ymmword ptr [rsi + 1c0h], ymm0
	vmovdqa ymmword ptr [rsi + 1e0h], ymm0
	vmovdqa ymmword ptr [rsi + 200h], ymm0
	vmovdqa ymmword ptr [rsi + 220h], ymm0
	vmovdqa ymmword ptr [rsi + 240h], ymm0
	vmovdqa ymmword ptr [rsi + 260h], ymm0
	
	add rsi, BUFFER_SIZE

	vmovdqa ymmword ptr [rsi + 000h], ymm0
	vmovdqa ymmword ptr [rsi + 020h], ymm0
	vmovdqa ymmword ptr [rsi + 040h], ymm0
	vmovdqa ymmword ptr [rsi + 060h], ymm0
	vmovdqa ymmword ptr [rsi + 080h], ymm0
	vmovdqa ymmword ptr [rsi + 0a0h], ymm0
	vmovdqa ymmword ptr [rsi + 0c0h], ymm0
	vmovdqa ymmword ptr [rsi + 0e0h], ymm0
	vmovdqa ymmword ptr [rsi + 100h], ymm0
	vmovdqa ymmword ptr [rsi + 120h], ymm0

	vmovdqa ymmword ptr [rsi + 140h], ymm0
	vmovdqa ymmword ptr [rsi + 160h], ymm0
	vmovdqa ymmword ptr [rsi + 180h], ymm0
	vmovdqa ymmword ptr [rsi + 1a0h], ymm0
	vmovdqa ymmword ptr [rsi + 1c0h], ymm0
	vmovdqa ymmword ptr [rsi + 1e0h], ymm0
	vmovdqa ymmword ptr [rsi + 200h], ymm0
	vmovdqa ymmword ptr [rsi + 220h], ymm0
	vmovdqa ymmword ptr [rsi + 240h], ymm0
	vmovdqa ymmword ptr [rsi + 260h], ymm0

	add rsi, BUFFER_SIZE

	vmovdqa ymmword ptr [rsi + 000h], ymm0
	vmovdqa ymmword ptr [rsi + 020h], ymm0
	vmovdqa ymmword ptr [rsi + 040h], ymm0
	vmovdqa ymmword ptr [rsi + 060h], ymm0
	vmovdqa ymmword ptr [rsi + 080h], ymm0
	vmovdqa ymmword ptr [rsi + 0a0h], ymm0
	vmovdqa ymmword ptr [rsi + 0c0h], ymm0
	vmovdqa ymmword ptr [rsi + 0e0h], ymm0
	vmovdqa ymmword ptr [rsi + 100h], ymm0
	vmovdqa ymmword ptr [rsi + 120h], ymm0

	vmovdqa ymmword ptr [rsi + 140h], ymm0
	vmovdqa ymmword ptr [rsi + 160h], ymm0
	vmovdqa ymmword ptr [rsi + 180h], ymm0
	vmovdqa ymmword ptr [rsi + 1a0h], ymm0
	vmovdqa ymmword ptr [rsi + 1c0h], ymm0
	vmovdqa ymmword ptr [rsi + 1e0h], ymm0
	vmovdqa ymmword ptr [rsi + 200h], ymm0
	vmovdqa ymmword ptr [rsi + 220h], ymm0
	vmovdqa ymmword ptr [rsi + 240h], ymm0
	vmovdqa ymmword ptr [rsi + 260h], ymm0

	ret

clear_sprite_buffer endp

;map_height=0
;map_width=0
;tile_height=0
;tile_width=0
;colour_depth=0
;tile_definition_count=0

;rept 4								; map_height
;	map_width=0
;	rept 4							; map_width
;		tile_height=0
;		rept 2						; tile_height
;			tile_width=0
;			rept 2					; tile_width
;				colour_depth=0
;				rept 4				; colour_depth
					;tile_definition_proc map_height, map_width, tile_height, tile_width, colour_depth, tile_definition_count

;					colour_depth=colour_depth+1
;					tile_definition_count=tile_definition_count+1
;				endm
;				tile_width=tile_width+1
;			endm
;			tile_height=tile_height+1
;		endm
;		map_width=map_width+1
;	endm	
;	map_height=map_height+1
;endm





align 8
vera_default_palette:
; display here as xRGB, but written to memory as little endian, so GBxR
dw 0000h, 0fffh, 0800h, 0afeh, 0c4ch, 00c5h, 000ah, 0ee7h, 0d85h, 0640h, 0f77h, 0333h, 0777h, 0af6h, 008fh, 0bbbh
dw 0000h, 0111h, 0222h, 0333h, 0444h, 0555h, 0666h, 0777h, 0888h, 0999h, 0aaah, 0bbbh, 0ccch, 0dddh, 0eeeh, 0fffh
dw 0211h, 0433h, 0644h, 0866h, 0a88h, 0c99h, 0fbbh, 0211h, 0422h, 0633h, 0844h, 0a55h, 0c66h, 0f77h, 0200h, 0411h
dw 0611h, 0822h, 0a22h, 0c33h, 0f33h, 0200h, 0400h, 0600h, 0800h, 0a00h, 0c00h, 0f00h, 0221h, 0443h, 0664h, 0886h
dw 0aa8h, 0cc9h, 0febh, 0211h, 0432h, 0653h, 0874h, 0a95h, 0cb6h, 0fd7h, 0210h, 0431h, 0651h, 0862h, 0a82h, 0ca3h
dw 0fc3h, 0210h, 0430h, 0640h, 0860h, 0a80h, 0c90h, 0fb0h, 0121h, 0343h, 0564h, 0786h, 09a8h, 0bc9h, 0dfbh, 0121h
dw 0342h, 0463h, 0684h, 08a5h, 09c6h, 0bf7h, 0120h, 0241h, 0461h, 0582h, 06a2h, 08c3h, 09f3h, 0120h, 0240h, 0360h
dw 0480h, 05a0h, 06c0h, 07f0h, 0121h, 0343h, 0465h, 0686h, 08a8h, 09cah, 0bfch, 0121h, 0242h, 0364h, 0485h, 05a6h
dw 06c8h, 07f9h, 0020h, 0141h, 0162h, 0283h, 02a4h, 03c5h, 03f6h, 0020h, 0041h, 0061h, 0082h, 00a2h, 00c3h, 00f3h
dw 0122h, 0344h, 0466h, 0688h, 08aah, 09cch, 0bffh, 0122h, 0244h, 0366h, 0488h, 05aah, 06cch, 07ffh, 0022h, 0144h
dw 0166h, 0288h, 02aah, 03cch, 03ffh, 0022h, 0044h, 0066h, 0088h, 00aah, 00cch, 00ffh, 0112h, 0334h, 0456h, 0668h
dw 088ah, 09ach, 0bcfh, 0112h, 0224h, 0346h, 0458h, 056ah, 068ch, 079fh, 0002h, 0114h, 0126h, 0238h, 024ah, 035ch
dw 036fh, 0002h, 0014h, 0016h, 0028h, 002ah, 003ch, 003fh, 0112h, 0334h, 0546h, 0768h, 098ah, 0b9ch, 0dbfh, 0112h
dw 0324h, 0436h, 0648h, 085ah, 096ch, 0b7fh, 0102h, 0214h, 0416h, 0528h, 062ah, 083ch, 093fh, 0102h, 0204h, 0306h
dw 0408h, 050ah, 060ch, 070fh, 0212h, 0434h, 0646h, 0868h, 0a8ah, 0c9ch, 0fbeh, 0211h, 0423h, 0635h, 0847h, 0a59h
dw 0c6bh, 0f7dh, 0201h, 0413h, 0615h, 0826h, 0a28h, 0c3ah, 0f3ch, 0201h, 0403h, 0604h, 0806h, 0a08h, 0c09h, 0f0bh

; 320 multiplication lut for the bitmap offset
align 8
bitmap_width_320_lut:
qword 0, 320, 640, 960, 1280, 1600, 1920, 2240, 2560, 2880, 3200, 3520, 3840, 4160, 4480, 4800
qword 5120, 5440, 5760, 6080, 6400, 6720, 7040, 7360, 7680, 8000, 8320, 8640, 8960, 9280, 9600, 9920
qword 10240, 10560, 10880, 11200, 11520, 11840, 12160, 12480, 12800, 13120, 13440, 13760, 14080, 14400, 14720, 15040
qword 15360, 15680, 16000, 16320, 16640, 16960, 17280, 17600, 17920, 18240, 18560, 18880, 19200, 19520, 19840, 20160
qword 20480, 20800, 21120, 21440, 21760, 22080, 22400, 22720, 23040, 23360, 23680, 24000, 24320, 24640, 24960, 25280
qword 25600, 25920, 26240, 26560, 26880, 27200, 27520, 27840, 28160, 28480, 28800, 29120, 29440, 29760, 30080, 30400
qword 30720, 31040, 31360, 31680, 32000, 32320, 32640, 32960, 33280, 33600, 33920, 34240, 34560, 34880, 35200, 35520
qword 35840, 36160, 36480, 36800, 37120, 37440, 37760, 38080, 38400, 38720, 39040, 39360, 39680, 40000, 40320, 40640
qword 40960, 41280, 41600, 41920, 42240, 42560, 42880, 43200, 43520, 43840, 44160, 44480, 44800, 45120, 45440, 45760
qword 46080, 46400, 46720, 47040, 47360, 47680, 48000, 48320, 48640, 48960, 49280, 49600, 49920, 50240, 50560, 50880
qword 51200, 51520, 51840, 52160, 52480, 52800, 53120, 53440, 53760, 54080, 54400, 54720, 55040, 55360, 55680, 56000
qword 56320, 56640, 56960, 57280, 57600, 57920, 58240, 58560, 58880, 59200, 59520, 59840, 60160, 60480, 60800, 61120
qword 61440, 61760, 62080, 62400, 62720, 63040, 63360, 63680, 64000, 64320, 64640, 64960, 65280, 65600, 65920, 66240
qword 66560, 66880, 67200, 67520, 67840, 68160, 68480, 68800, 69120, 69440, 69760, 70080, 70400, 70720, 71040, 71360
qword 71680, 72000, 72320, 72640, 72960, 73280, 73600, 73920, 74240, 74560, 74880, 75200, 75520, 75840, 76160, 76480
qword 76800, 77120, 77440, 77760, 78080, 78400, 78720, 79040, 79360, 79680, 80000, 80320, 80640, 80960, 81280, 81600
qword 81920, 82240, 82560, 82880, 83200, 83520, 83840, 84160, 84480, 84800, 85120, 85440, 85760, 86080, 86400, 86720
qword 87040, 87360, 87680, 88000, 88320, 88640, 88960, 89280, 89600, 89920, 90240, 90560, 90880, 91200, 91520, 91840
qword 92160, 92480, 92800, 93120, 93440, 93760, 94080, 94400, 94720, 95040, 95360, 95680, 96000, 96320, 96640, 96960
qword 97280, 97600, 97920, 98240, 98560, 98880, 99200, 99520, 99840, 100160, 100480, 100800, 101120, 101440, 101760, 102080
qword 102400, 102720, 103040, 103360, 103680, 104000, 104320, 104640, 104960, 105280, 105600, 105920, 106240, 106560, 106880, 107200
qword 107520, 107840, 108160, 108480, 108800, 109120, 109440, 109760, 110080, 110400, 110720, 111040, 111360, 111680, 112000, 112320
qword 112640, 112960, 113280, 113600, 113920, 114240, 114560, 114880, 115200, 115520, 115840, 116160, 116480, 116800, 117120, 117440
qword 117760, 118080, 118400, 118720, 119040, 119360, 119680, 120000, 120320, 120640, 120960, 121280, 121600, 121920, 122240, 122560
qword 122880, 123200, 123520, 123840, 124160, 124480, 124800, 125120, 125440, 125760, 126080, 126400, 126720, 127040, 127360, 127680
qword 128000, 128320, 128640, 128960, 129280, 129600, 129920, 130240, 130560, 130880, 131200, 131520, 131840, 132160, 132480, 132800
qword 133120, 133440, 133760, 134080, 134400, 134720, 135040, 135360, 135680, 136000, 136320, 136640, 136960, 137280, 137600, 137920
qword 138240, 138560, 138880, 139200, 139520, 139840, 140160, 140480, 140800, 141120, 141440, 141760, 142080, 142400, 142720, 143040
qword 143360, 143680, 144000, 144320, 144640, 144960, 145280, 145600, 145920, 146240, 146560, 146880, 147200, 147520, 147840, 148160
qword 148480, 148800, 149120, 149440, 149760, 150080, 150400, 150720, 151040, 151360, 151680, 152000, 152320, 152640, 152960, 153280
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600
qword 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600, 153600

align 8
bitmap_width_640_lut:
qword 0, 640, 1280, 1920, 2560, 3200, 3840, 4480, 5120, 5760, 6400, 7040, 7680, 8320, 8960, 9600
qword 10240, 10880, 11520, 12160, 12800, 13440, 14080, 14720, 15360, 16000, 16640, 17280, 17920, 18560, 19200, 19840
qword 20480, 21120, 21760, 22400, 23040, 23680, 24320, 24960, 25600, 26240, 26880, 27520, 28160, 28800, 29440, 30080
qword 30720, 31360, 32000, 32640, 33280, 33920, 34560, 35200, 35840, 36480, 37120, 37760, 38400, 39040, 39680, 40320
qword 40960, 41600, 42240, 42880, 43520, 44160, 44800, 45440, 46080, 46720, 47360, 48000, 48640, 49280, 49920, 50560
qword 51200, 51840, 52480, 53120, 53760, 54400, 55040, 55680, 56320, 56960, 57600, 58240, 58880, 59520, 60160, 60800
qword 61440, 62080, 62720, 63360, 64000, 64640, 65280, 65920, 66560, 67200, 67840, 68480, 69120, 69760, 70400, 71040
qword 71680, 72320, 72960, 73600, 74240, 74880, 75520, 76160, 76800, 77440, 78080, 78720, 79360, 80000, 80640, 81280
qword 81920, 82560, 83200, 83840, 84480, 85120, 85760, 86400, 87040, 87680, 88320, 88960, 89600, 90240, 90880, 91520
qword 92160, 92800, 93440, 94080, 94720, 95360, 96000, 96640, 97280, 97920, 98560, 99200, 99840, 100480, 101120, 101760
qword 102400, 103040, 103680, 104320, 104960, 105600, 106240, 106880, 107520, 108160, 108800, 109440, 110080, 110720, 111360, 112000
qword 112640, 113280, 113920, 114560, 115200, 115840, 116480, 117120, 117760, 118400, 119040, 119680, 120320, 120960, 121600, 122240
qword 122880, 123520, 124160, 124800, 125440, 126080, 126720, 127360, 128000, 128640, 129280, 129920, 130560, 131200, 131840, 132480
qword 133120, 133760, 134400, 135040, 135680, 136320, 136960, 137600, 138240, 138880, 139520, 140160, 140800, 141440, 142080, 142720
qword 143360, 144000, 144640, 145280, 145920, 146560, 147200, 147840, 148480, 149120, 149760, 150400, 151040, 151680, 152320, 152960
qword 153600, 154240, 154880, 155520, 156160, 156800, 157440, 158080, 158720, 159360, 160000, 160640, 161280, 161920, 162560, 163200
qword 163840, 164480, 165120, 165760, 166400, 167040, 167680, 168320, 168960, 169600, 170240, 170880, 171520, 172160, 172800, 173440
qword 174080, 174720, 175360, 176000, 176640, 177280, 177920, 178560, 179200, 179840, 180480, 181120, 181760, 182400, 183040, 183680
qword 184320, 184960, 185600, 186240, 186880, 187520, 188160, 188800, 189440, 190080, 190720, 191360, 192000, 192640, 193280, 193920
qword 194560, 195200, 195840, 196480, 197120, 197760, 198400, 199040, 199680, 200320, 200960, 201600, 202240, 202880, 203520, 204160
qword 204800, 205440, 206080, 206720, 207360, 208000, 208640, 209280, 209920, 210560, 211200, 211840, 212480, 213120, 213760, 214400
qword 215040, 215680, 216320, 216960, 217600, 218240, 218880, 219520, 220160, 220800, 221440, 222080, 222720, 223360, 224000, 224640
qword 225280, 225920, 226560, 227200, 227840, 228480, 229120, 229760, 230400, 231040, 231680, 232320, 232960, 233600, 234240, 234880
qword 235520, 236160, 236800, 237440, 238080, 238720, 239360, 240000, 240640, 241280, 241920, 242560, 243200, 243840, 244480, 245120
qword 245760, 246400, 247040, 247680, 248320, 248960, 249600, 250240, 250880, 251520, 252160, 252800, 253440, 254080, 254720, 255360
qword 256000, 256640, 257280, 257920, 258560, 259200, 259840, 260480, 261120, 261760, 262400, 263040, 263680, 264320, 264960, 265600
qword 266240, 266880, 267520, 268160, 268800, 269440, 270080, 270720, 271360, 272000, 272640, 273280, 273920, 274560, 275200, 275840
qword 276480, 277120, 277760, 278400, 279040, 279680, 280320, 280960, 281600, 282240, 282880, 283520, 284160, 284800, 285440, 286080
qword 286720, 287360, 288000, 288640, 289280, 289920, 290560, 291200, 291840, 292480, 293120, 293760, 294400, 295040, 295680, 296320
qword 296960, 297600, 298240, 298880, 299520, 300160, 300800, 301440, 302080, 302720, 303360, 304000, 304640, 305280, 305920, 306560
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200
qword 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200, 307200

align 8
should_display_table:
; 0 - dont render
; 1 - pull from buffer
; 2 - normal
; 3 - render to buffer
; 4 - render sprites to buffer
; 5 - line reset
; 6 - screen reset
; 7 - scale reset

REPT 479
	REPT 640
		db 2
	ENDM
	REPT 158
		db 3
	ENDM
	db 0
	db 5
ENDM

; last visible line, dont render to the buffer
REPT 640
	db 1
ENDM
REPT 159
	db 0
ENDM
db 0

; 480 lines done, end of visible area
REPT 43
	REPT 800
		db 0
	ENDM
ENDM

; second to last line
REPT 799
	db 0
ENDM
db 7	; scale reset + line end


; last line, render to the buffer
REPT 640
	db 3
ENDM
REPT 158
	db 3
ENDM
db 0
db 6

.code
