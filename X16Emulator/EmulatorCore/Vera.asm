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
; ----------------------------------------------------
; VERA
; ----------------------------------------------------

; Cpu emulation:
; rax  : scratch
; rbx  : scratch
; rcx  : scratch
; rdx  : state object 
; rdi  : current memory context
; rsi  : scratch
; r8b  : a
; r9b  : x
; r10b : y
; r11w : PC
; r12  : scratch
; r13  : scratch / use to indicate vera data0 or 1 read
; r14  : Clock Ticks
; r15  : Flags

include Constants.inc
include State.asm
include Vera_Display.asm
include Vera_Audio.asm
include Vera_Render_Layers.asm

vera_setaddress_0 macro 
	local search_loop, match, not_negative

	xor r12, r12						; use r12 to store if decr should be set
	mov rax, [rdx].state.data0_step

	test rax, rax
	jns not_negative

	mov r12b, 1000b
	neg rax

not_negative:

	xor rbx, rbx
	lea rsi, vera_step_table

search_loop:
	cmp ax, word ptr [rsi+rbx]
	je match
	add rbx, 2
	cmp rbx, 20h
	jne search_loop

match:
	and rbx, 11110b						; mask off index step (nof 0x0t as we x2 this value earlier)
	shl rbx, 4+8+8-1					; shift to the correct position for the registers
	mov rdi, [rdx].state.data0_address
	or rdi, rbx
	
	mov rsi, [rdx].state.memory_ptr
	mov [rsi+ADDRx_L], di

	shr rdi, 16
	or dil, r12b						; or on the DECR bit
	mov [rsi+ADDRx_H], dil
endm

vera_setaddress_1 macro 
	local search_loop, match

	xor r12, r12						; use r12 to store if decr should be set
	mov rax, [rdx].state.data1_step

	test rax, rax
	jns not_negative

	mov r12b, 1000b
	neg rax

not_negative:

	xor rbx, rbx
	lea rsi, vera_step_table

search_loop:
	cmp ax, word ptr [rsi+rbx]
	je match
	add rbx, 2
	cmp rbx, 20h
	jne search_loop

match:
	and rbx, 11110b
	shl rbx, 4+8+8-1
	mov rdi, [rdx].state.data1_address
	or rdi, rbx
	
	mov rsi, [rdx].state.memory_ptr
	mov [rsi+ADDRx_L], di

	shr rdi, 16
	or dil, r12b						; or on the DECR bit
	mov [rsi+ADDRx_H], dil
endm

set_layer0_jump macro
	local done, bitmap

	mov rsi, [rdx].state.memory_ptr

	; get generic map fetch proc
	movzx rax, byte ptr [rsi + L0_CONFIG]
	and rax, 0f0h			; map height / width
	shr eax, 2
	movzx rbx, byte ptr [rsi + L0_TILEBASE]
	and ebx, 011b			; tile height / width
	or eax, ebx
	lea rbx, get_map_jump
	add rbx, [rbx + rax * 8]
	mov qword ptr [rdx].state.layer0_fetchmap, rbx

	
	movzx rax, [rdx].state.layer0_bitmapMode
	test rax, rax
	jnz bitmap

	; get generic data fetch proc
	movzx rax, byte ptr [rsi + L0_CONFIG]
	and eax, 011b			;  mask the colour depth
	movzx rbx, byte ptr [rsi + L0_TILEBASE]
	and ebx, 011b			; tile height / width
	shl ebx, 2
	or eax, ebx
	lea rbx, get_tile_jump
	add rbx, [rbx + rax * 8]
	mov qword ptr [rdx].state.layer0_fetchtile, rbx
	jmp done

	; get renderer

	;movzx rax, byte ptr [rsi + L0_CONFIG]
	;and rax, 11110011b
	;movzx rbx, byte ptr [rsi + L0_TILEBASE]
	;and rbx, 011b
	;shl rbx, 2
	;or rax, rbx
	;lea rbx, get_tile_definition_jump
	;add rbx, [rbx + rax * 8]
	;mov qword ptr [rdx].state.layer0_jmp, rbx

bitmap:
	movzx rax, byte ptr [rsi + L0_CONFIG]
	and rax, 011b
	shl rax, 1
	movzx rbx, byte ptr [rsi + L0_TILEBASE]
	and rbx, 01b
	or rax, rbx

	lea rbx, get_bitmap_jump
	add rbx, [rbx + rax * 8]
	mov qword ptr [rdx].state.layer0_fetchtile, rbx

done:
endm

set_layer1_jump macro
	local done, bitmap

	mov rsi, [rdx].state.memory_ptr

	; get generic map fetch proc
	movzx rax, byte ptr [rsi + L1_CONFIG]
	and rax, 0f0h			; map height / width
	shr eax, 2
	movzx rbx, byte ptr [rsi + L1_TILEBASE]
	and ebx, 011b			; tile height / width
	or eax, ebx
	lea rbx, get_map_jump
	add rbx, [rbx + rax * 8]
	mov qword ptr [rdx].state.layer1_fetchmap, rbx

	movzx rax, [rdx].state.layer1_bitmapMode
	test rax, rax
	jnz bitmap

	; get generic data fetch proc
	movzx rax, byte ptr [rsi + L1_CONFIG]
	and eax, 011b
	movzx rbx, byte ptr [rsi + L1_TILEBASE]
	and ebx, 011b
	shl ebx, 2
	or eax, ebx
	lea rbx, get_tile_jump
	add rbx, [rbx + rax * 8]
	mov qword ptr [rdx].state.layer1_fetchtile, rbx
	jmp done


	; get renderer

	;movzx rax, byte ptr [rsi + L1_CONFIG]
	;and rax, 11110011b
	;movzx rbx, byte ptr [rsi + L1_TILEBASE]
	;and rbx, 011b
	;shl rbx, 2
	;or rax, rbx
	;lea rbx, get_tile_definition_jump
	;add rbx, qword ptr [rbx + rax * 8]
	;mov qword ptr [rdx].state.layer1_jmp, rbx

bitmap:
	movzx rax, byte ptr [rsi + L1_CONFIG]
	and rax, 011b
	shl rax, 1
	movzx rbx, byte ptr [rsi + L1_TILEBASE]
	and rbx, 01b
	or rax, rbx

	lea rbx, get_bitmap_jump
	add rbx, qword ptr [rbx + rax * 8]
	mov qword ptr [rdx].state.layer1_fetchtile, rbx

done:
endm

layer0_tileshifts macro
	; Multipliers
	; Y tile height
	lea rax, vera_map_shift_table
	movzx rbx, byte ptr [rdx].state.layer0_mapWidth
	mov bx, word ptr [rax + rbx * 2]
	mov word ptr [rdx].state.layer0_map_hshift, bx

	movzx rbx, byte ptr [rdx].state.layer0_mapHeight
	mov bx, word ptr [rax + rbx * 2]
	mov word ptr [rdx].state.layer0_map_vshift, bx

	lea rax, vera_tile_shift_table
	movzx rbx, byte ptr [rdx].state.layer0_tileWidth
	mov bx, word ptr [rax + rbx * 2]
	mov word ptr [rdx].state.layer0_tile_hshift, bx

	movzx rbx, byte ptr [rdx].state.layer0_tileHeight
	mov bx, word ptr [rax + rbx * 2]
	mov word ptr [rdx].state.layer0_tile_vshift, bx

	xor rbx, rbx
	xor rax, rax
endm

layer1_tileshifts macro
	; Multipliers
	; Y tile height
	lea rax, vera_map_shift_table
	movzx rbx, byte ptr [rdx].state.layer1_mapWidth
	mov bx, word ptr [rax + rbx * 2]
	mov word ptr [rdx].state.layer1_map_hshift, bx

	movzx rbx, byte ptr [rdx].state.layer1_mapHeight
	mov bx, word ptr [rax + rbx * 2]
	mov word ptr [rdx].state.layer1_map_vshift, bx

	lea rax, vera_tile_shift_table
	movzx rbx, byte ptr [rdx].state.layer1_tileWidth
	mov bx, word ptr [rax + rbx * 2]
	mov word ptr [rdx].state.layer1_tile_hshift, bx

	movzx rbx, byte ptr [rdx].state.layer1_tileHeight
	mov bx, word ptr [rax + rbx * 2]
	mov word ptr [rdx].state.layer1_tile_vshift, bx

	xor rbx, rbx
	xor rax, rax
endm

; initialise colours etc
; rdx points to cpu state
vera_init proc
	;
	; Set to be drawing, as we'll start at 0,0
	;
	mov byte ptr [rdx].state.drawing, 1
	;mov dword ptr [rdx].state.buffer_render_position, 010000000000b
	mov [rdx].state.frame_count, 0		; always start with 0 frames, as we use this for timing.

	;
	; DATA0\1
	;
	mov rsi, [rdx].state.memory_ptr
	mov rdi, [rdx].state.vram_ptr
	
	mov rax, [rdx].state.data0_address
	mov bl, byte ptr [rdi+rax]
	mov byte ptr [rsi+DATA0], bl

	mov rax, [rdx].state.data1_address
	mov bl, byte ptr [rdi+rax]
	mov byte ptr [rsi+DATA1], bl

	;
	; AddrSel CTRL + ADDR_x
	;
	cmp [rdx].state.addrsel, 0
	jne set_address1

	; Set Address 0 - init to ctrl as 0
	vera_setaddress_0
	mov byte ptr [rsi+CTRL], 0
	jmp addr_done

set_address1:
	vera_setaddress_1
	mov byte ptr [rsi+CTRL], 1
	
addr_done:

	mov rbx, 03fffeh
	mov rax, 03ffffh
	test [rdx].state.fx_4bit_mode, 1
	cmovnz rbx, rax
	mov [rdx].state.data_mask, rbx

	;
	; DcSel CTRL
	;
	movzx r13d, [rdx].state.dcsel
	mov eax, r13d
	shl eax, 1
	and byte ptr [rsi+CTRL], 01h
	or byte ptr [rsi+CTRL], al		

	;
	; DC_xxx + DC_ Video Settings
	;
	lea rax, dc_sel_table
	add rax, [rax + r13 * 8]
	jmp rax

dc_sel0:
	xor rax, rax
	mov al, byte ptr [rdx].state.sprite_enable
	shl rax, 6
	mov bl, byte ptr [rdx].state.layer1_enable
	shl rbx, 5
	or rax, rbx
	mov bl, byte ptr [rdx].state.layer0_enable
	shl rbx, 4
	or rax, rbx

	mov ebx, dword ptr [rdx].state.video_output
	and ebx, 0fh
	or rax, rbx

	mov byte ptr [rsi+DC_VIDEO], al

	mov eax, dword ptr [rdx].state.dc_hscale
	shr rax, 9
	mov byte ptr [rsi+DC_HSCALE], al

	mov eax, dword ptr [rdx].state.dc_vscale
	shr rax, 9
	mov byte ptr [rsi+DC_VSCALE], al

	mov al, byte ptr [rdx].state.dc_border
	mov byte ptr [rsi+DC_BORDER], al

	jmp dc_done

dc_sel1:

	mov ax, word ptr [rdx].state.dc_hstart
	shr ax, 2
	mov byte ptr [rsi+DC_HSTART], al

	mov ax, word ptr [rdx].state.dc_hstop
	shr ax, 2
	mov byte ptr [rsi+DC_HSTOP], al

	mov ax, word ptr [rdx].state.dc_vstart
	shr ax, 1
	mov byte ptr [rsi+DC_VSTART], al

	mov ax, word ptr [rdx].state.dc_vstop
	shr ax, 1
	mov byte ptr [rsi+DC_VSTOP], al

	jmp dc_done

version:
not_supported:
	; default is the version

	mov byte ptr [rsi+DC_VER0], 056h
	mov byte ptr [rsi+DC_VER1], VERA_VERSION_L
	mov byte ptr [rsi+DC_VER2], VERA_VERSION_M
	mov byte ptr [rsi+DC_VER3], VERA_VERSION_H
	
	jmp dc_done

dc_done:

	;
	; Layer 0
	;

	; Config

	xor rax, rax
	mov al, byte ptr [rdx].state.layer0_mapHeight
	and rax, 00000011b
	shl rax, 6
	mov bl, byte ptr [rdx].state.layer0_mapWidth
	and rbx, 00000011b
	shl rbx, 4
	or al, bl
	mov bl, byte ptr [rdx].state.layer0_bitmapMode
	and rbx, 00000001b
	shl rbx, 2
	or al, bl
	mov bl, byte ptr [rdx].state.layer0_colourDepth
	and rbx, 00000011b
	or al, bl
	mov ebx, dword ptr [rdx].state.layer1_t256c
	shl ebx, 3
	or al, bl

	mov byte ptr [rsi+L0_CONFIG], al
	and al, 00001111b
	lea rbx, layer0_render_jump
	add rbx, qword ptr [rbx + rax * 8]
	mov qword ptr [rdx].state.layer0_renderer, rbx

	;mov word ptr [rdx].state.layer0_config, ax

	; Map Base Address
	mov eax, dword ptr [rdx].state.layer0_mapAddress
	shr rax, 9
	mov byte ptr [rsi+L0_MAPBASE], al

	; Tile Base Address + Tile Height\Width
	mov eax, dword ptr [rdx].state.layer0_tileAddress
	shr rax, 9
	and rax, 11111100b
	mov bl, byte ptr [rdx].state.layer0_tileHeight
	and bl, 00000001b
	shl bl, 1
	or al, bl
	mov bl, byte ptr [rdx].state.layer0_tileWidth
	and bl, 00000001b
	or al, bl
	mov byte ptr [rsi+L0_TILEBASE], al

	layer0_tileshifts

	; HScroll
	mov ax, word ptr [rdx].state.layer0_hscroll
	and ax, 0fffh
	mov byte ptr [rsi+L0_HSCROLL_L], al
	mov byte ptr [rsi+L0_HSCROLL_H], ah

	; VScroll
	mov ax, word ptr [rdx].state.layer0_vscroll
	and ax, 0fffh
	mov byte ptr [rsi+L0_VSCROLL_L], al
	mov byte ptr [rsi+L0_VSCROLL_H], ah

	;
	; Layer 1
	;

	; Config

	xor rax, rax
	mov al, byte ptr [rdx].state.layer1_mapHeight
	and rax, 00000011b
	shl rax, 6
	mov bl, byte ptr [rdx].state.layer1_mapWidth
	and rbx, 00000011b
	shl rbx, 4
	or al, bl
	mov bl, byte ptr [rdx].state.layer1_bitmapMode
	and rbx, 00000001b
	shl rbx, 2
	or al, bl
	mov bl, byte ptr [rdx].state.layer1_colourDepth
	and rbx, 00000011b
	or al, bl
	mov ebx, dword ptr [rdx].state.layer1_t256c
	shl ebx, 3
	or al, bl

	mov byte ptr [rsi+L1_CONFIG], al
	and al, 00001111b
	lea rbx, layer1_render_jump
	add rbx, qword ptr [rbx + rax * 8]
	mov qword ptr [rdx].state.layer1_renderer, rbx
	;mov word ptr [rdx].state.layer1_config, ax

	; Map Base Address
	mov eax, dword ptr [rdx].state.layer1_mapAddress
	shr rax, 9
	mov byte ptr [rsi+L1_MAPBASE], al

	; Tile Base Address + Tile Height\Width
	mov eax, dword ptr [rdx].state.layer1_tileAddress
	shr rax, 9
	and rax, 11111100b
	mov bl, byte ptr [rdx].state.layer1_tileHeight
	and bl, 00000001b
	shl bl, 1
	or al, bl
	mov bl, byte ptr [rdx].state.layer1_tileWidth
	and bl, 00000001b
	or al, bl
	mov byte ptr [rsi+L1_TILEBASE], al

	layer1_tileshifts

	; HScroll
	mov ax, word ptr [rdx].state.layer1_hscroll
	and ax, 0fffh
	mov byte ptr [rsi+L1_HSCROLL_L], al
	mov byte ptr [rsi+L1_HSCROLL_H], ah

	; VScroll
	mov ax, word ptr [rdx].state.layer1_vscroll
	and ax, 0fffh
	mov byte ptr [rsi+L1_VSCROLL_L], al
	mov byte ptr [rsi+L1_VSCROLL_H], ah

	; Interrupt Flags
	mov al, byte ptr [rdx].state.interrupt_vsync
	mov bl, byte ptr [rdx].state.interrupt_line
	shl bl, 1
	or al, bl
	mov bl, byte ptr [rdx].state.interrupt_spcol
	shl bl, 2
	or al, bl
	mov bl, byte ptr [rdx].state.interrupt_aflow
	shl bl, 3
	or al, bl
	mov bx, word ptr [rdx].state.interrupt_linenum

	mov byte ptr [rsi+IRQLINE_L], bl

	and bx, 100h
	shr bx, 1
	or al, bl
	mov byte ptr [rsi+IEN], al

	; todo: Add setting ISR based on hit parameters

	; Layer 0 jump
	set_layer0_jump

	; Layer 1 jump
	set_layer1_jump

	; PCM Set CTRL
	mov eax, [rdx].state.pcm_bufferread
	mov ebx, [rdx].state.pcm_bufferwrite
	movzx r13, byte ptr [rsi + AUDIO_CTRL]
	and r13d, 3fh

	cmp eax, ebx
	jne check_full

	or r13d, 40h		; set empty bit
	jmp pcm_done

	check_full:

	inc ebx
	and ebx, 0fffh
	cmp ebx, eax
	jne pcm_done

	or r13d, 80h		; set full bit

	pcm_done:

	mov byte ptr [rsi + AUDIO_CTRL], r13b

	push rsi
	call vera_init_psg
	pop rsi

	mov eax, [rdx].state.initial_startup
	test eax, eax
	jz not_initial

	or byte ptr [rsi + ISR], 08h	; set AFLOW but only on initial

	jmp vera_initialise_palette

not_initial:
	ret



dc_sel_table:
	qword dc_sel0 - dc_sel_table
	qword dc_sel1 - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table

	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table

	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table

	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table

	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table

	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table

	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table

	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword version - dc_sel_table

vera_init endp

;
; rdi			address
; rax			base address
; r13b			value
;
; Todo, add PSG\Sprite changes if reqd
;
vera_dataupdate_stuctures macro
	local skip, xx_red, sprite_change, psg_change
	push rsi
	push rax

	cmp rdi, 1f9bfh
	jl skip

	cmp rdi, 1fa00h
	jb psg_change

	cmp rdi, 1fbffh
	ja sprite_change

	mov rsi, [rdx].state.palette_ptr
	mov rax, rdi
	sub rax, 01fa00h
	and rax, 0fffeh							; take off the low bit, as we want the colour index
	mov ecx, [rsi + rax * 2]

	bt rdi, 0
	jc xx_red

	; r13 is GB
	and rcx, 0ff0000ffh						; take GB from current colour
	
	mov r12, r13
	and r12, 00fh							; Isolate B
	shl r12, 16
	or rcx, r12								; or in first nibble
	shl r12, 4
	or rcx, r12								; or in second nibble
	
	mov r12, r13
	and r12, 0f0h							; Isolate G
	shl r12, 4		
	or rcx, r12								; or in first nibble
	shl r12, 4
	or rcx, r12								; or in second nibble

	mov dword ptr [rsi + rax * 2], ecx		; persist
;	pop rax
	jmp skip
xx_red:
	; r13 is xR
	
	and rcx, 0ffffff00h						; take R from current colour

	mov r12, r13
	and r12, 00fh
	or rcx, r12								; or in first nibble
	shl r12, 4
	or rcx, r12								; or in second nibble

	mov dword ptr [rsi + rax * 2], ecx
;	pop rax
	jmp skip
sprite_change:
	push rbx
	call sprite_update_registers
	pop rbx
;	pop rax
	jmp skip

psg_change:
	push rbx
	call psg_update_registers
	pop rbx
;	pop rax

skip:
	pop rax
	pop rsi
endm

vera_write_dataport macro
local cannot_cache_write, cache_write, fx_4bit_pixels, no_transparancy, done, no_multiplication
	cmp rdi, 01f9c0h
	jge cannot_cache_write

	test [rdx].state.fx_cache_write, 1
	jnz cache_write
cannot_cache_write:
	movzx r13, byte ptr [rsi+rbx]			; get value that has been written
	mov byte ptr [rax+rdi], r13b			; store in vram
	vera_dataupdate_stuctures
	jmp done

cache_write:

	mov r12d, [rdx].state.fx_cache			; get the cache value

	; check if multiplication is set
	test [rdx].state.fx_multiplier_enable, 1
	jz no_multiplication

	;int 3

	mov ecx, r12d
	shr ecx, 16
	movsx r12d, r12w
	movsx ecx, cx
	imul r12d, ecx

	mov ecx, [rdx].state.fx_accumulator
	lea r13, [r12 + rcx]					; addition
	sub ecx, r12d							; sub
	mov r12d, ecx

	test [rdx].state.fx_accumulate_direction, 1
	cmovz r12d, r13d						; is sub

no_multiplication:
	push rdi
	and rdi, 1fffch							; cache writes are 4 byte alligned

	movzx r13, byte ptr [rsi+rbx]			; get value that has been written

	test [rdx].state.fx_transparancy, 1
	jz no_transparancy

	push rsi
	push r8

	xor ecx, ecx							; we'll use ecx for the mask
	xor r8d, r8d							; resuling mask

	test [rdx].state.fx_4bit_mode, 1
	jnz fx_4bit_pixels

	; check for transparent pixels
	mov r13d, 0ffh
	test r12d, r13d
	cmovnz r8d, r13d

	shl r13d, 8								; now $ff00
	test r12d, r13d							; check cache byte vs mask
	cmovnz ecx, r13d						; if it has a value, its not zero to set exc to mask
	or r8d, ecx								; or on the result to the total

	xor ecx, ecx

	shl r13d, 8								; now $ff0000
	test r12d, r13d
	cmovnz ecx, r13d
	or r8d, ecx

	xor ecx, ecx

	shl r13d, 8								; now $ff000000
	test r12d, r13d
	cmovnz ecx, r13d
	or r8d, ecx								; r8d now contains the full mask of actual values

	xor r8d, 0ffffffffh						; invert as we apply this tot he current vaule

	mov esi, [rax+rdi]						; current value
	and esi, r8d							; remove non-zero items
	or r12d, esi							; or in the values from memory

	pop r8
	pop rsi

	mov [rax+rdi], r12d						; update vram

	pop rdi
	jmp done

fx_4bit_pixels:

	; check for transparent pixels
	mov r13d, 0fh
	test r12d, r13d
	cmovnz r8d, r13d

	shl r13d, 4								; now $f0
	test r12d, r13d							; check cache byte vs mask
	cmovnz ecx, r13d						; if it has a value, its not zero to set exc to mask
	or r8d, ecx								; or on the result to the total

	xor ecx, ecx

	shl r13d, 4								; now $f00
	test r12d, r13d
	cmovnz ecx, r13d
	or r8d, ecx

	xor ecx, ecx

	shl r13d, 4								; now $f000
	test r12d, r13d
	cmovnz ecx, r13d
	or r8d, ecx

	xor ecx, ecx

	shl r13d, 4								; now $f-0000
	test r12d, r13d
	cmovnz ecx, r13d
	or r8d, ecx

	xor ecx, ecx

	shl r13d, 4								; now $f0-0000
	test r12d, r13d
	cmovnz ecx, r13d
	or r8d, ecx

	xor ecx, ecx

	shl r13d, 4								; now $f00-0000
	test r12d, r13d
	cmovnz ecx, r13d
	or r8d, ecx

	xor ecx, ecx

	shl r13d, 4								; now $f000-0000
	test r12d, r13d
	cmovnz ecx, r13d
	or r8d, ecx								; r8d now contains the full mask of actual values

	xor r8d, 0ffffffffh						; invert as we apply this tot he current vaule

	mov esi, [rax+rdi]						; current value
	and esi, r8d							; remove non-zero items
	or r12d, esi							; or in the values from memory

	pop r8
	pop rsi

	mov [rax+rdi], r12d						; update vram

	pop rdi
	jmp done

no_transparancy:

	lea rcx, data_cache_mask
	mov r13, [rcx + r13 * 4]				; r13 is the byte written

	and r12d, r13d							; and value with cache value

	xor r13d, 0ffffffffh					; invert to use against value in ram

	mov ecx, [rax+rdi]						; get current value
	and ecx, r13d							; mask

	or r12d, ecx							; and combine the two masked variables

	mov [rax+rdi], r12d						; update vram

	pop rdi

done:
endm

vera_read_dataport_cache macro
local done, fx_4bit_pixels, fx_2_byte, cache_done
		; if cache fill is enabled need to add the current data 0 value to the cache
		test [rdx].state.fx_cache_fill, 1
		jz done

		movzx r13, byte ptr [rsi+rbx]

		push rax

		mov r12d, [rdx].state.fx_cache_fill_shift

		test [rdx].state.fx_4bit_mode, 1
		jnz fx_4bit_pixels

		mov eax, 0ffh
		shlx eax, eax, r12d						; mask
		shlx r13d, r13d, r12d					; value
		xor eax, 0ffffffffh

		test [rdx].state.fx_2byte_cache_incr, 1
		jnz fx_2_byte

		; store shift while its in r12
		add r12d, 8
		and r12d, 01fh
		mov [rdx].state.fx_cache_fill_shift, r12d

		mov r12d, [rdx].state.fx_cache
		and r12d, eax							; mask out
		or r12d, r13d							; add value
		mov [rdx].state.fx_cache, r12d

		; update cache index
		mov r12d, [rdx].state.fx_cache_index
		add r12d, 2
		and r12d, 0111b
		mov [rdx].state.fx_cache_index, r12d

		jmp cache_done

	fx_2_byte:
		; if two byte increment is one, we just need to flip bottom index, rather than inc

		; store shift while its in r12
		xor r12d, 8
		mov [rdx].state.fx_cache_fill_shift, r12d

		mov r12d, [rdx].state.fx_cache
		and r12d, eax							; mask out
		or r12d, r13d							; add value
		mov [rdx].state.fx_cache, r12d

		; update cache index
		mov r12d, [rdx].state.fx_cache_index
		xor r12d, 2
		mov [rdx].state.fx_cache_index, r12d

		jmp cache_done

	fx_4bit_pixels:

		mov eax, 00fh							
		and r13d, 0f0h							; isolate bottom 4 bits and move into position
		shr r13d, 4
		xor r12d, 4								; adjust for endieness of 4 bit data

		shlx eax, eax, r12d						; mask
		shlx r13d, r13d, r12d					; value
		xor eax, 0ffffffffh

		; store shift while its in r12
		xor r12d, 4								; revert to store
		add r12d, 4
		and r12d, 01fh
		mov [rdx].state.fx_cache_fill_shift, r12d

		mov r12d, [rdx].state.fx_cache
		and r12d, eax							; mask out
		or r12d, r13d							; add value
		mov [rdx].state.fx_cache, r12d

		; update cache index
		mov r12d, [rdx].state.fx_cache_index
		inc r12d
		and r12d, 0111b
		mov [rdx].state.fx_cache_index, r12d

	cache_done:
		pop rax

	done:
endm


; rbx			address
; [rsi+rbx]		output location in main memory
; 
; should only be called if data0\data1 is read\written.
vera_dataaccess_body macro doublestep, write_value
	local cache_write_0, done_0, no_transparancy_0, fx_4bit_pixels_0, cache_write_1, done_1, no_transparancy_1 ,cache_done, no_fx_step_1, polyfill_1, not_fx_1, affine_1

	mov rax, [rdx].state.vram_ptr				; get value from vram
	push rsi
	mov rsi, [rdx].state.memory_ptr				; we need memory context;

	cmp rbx, DATA0
	jne step_data1

	mov rdi, [rdx].state.data0_address

	if write_value eq 1 and doublestep eq 0
		vera_write_dataport
	endif

	if write_value eq 0
		vera_read_dataport_cache
	endif

	add rdi, [rdx].state.data0_step
	and rdi, 1ffffh								; mask off high bits so we wrap

	if doublestep eq 1
		if write_value eq 1
			vera_write_dataport
		endif

		add rdi, [rdx].state.data0_step			; perform second step
		and rdi, 1ffffh							; mask off high bits so we wrap
	endif

	mov [rdx].state.data0_address, rdi

	mov r13b, byte ptr [rax+rdi]
	mov [rsi+rbx], r13b							; store in ram

	xor r13, r13								; clear r13b, as we use this to detect if we need to call vera

	cmp [rdx].state.addrsel, 0
	je set_data0_address
	pop rsi
	ret

set_data0_address:
		
	;mov rsi, [rdx].state.memory_ptr
	mov word ptr [rsi + ADDRx_L], di			; write M and L bytes

	;shr rdi, 8
	;mov byte ptr [rsi + ADDRx_M], dil

	shr rdi, 16									; need to add bottom bit of the address, so shift and mask
	and rdi, 1
	movzx rax, byte ptr [rsi + ADDRx_H]			; Add on stepping nibble
	and eax, 0f8h								; mask off what isnt changable
	or rdi, rax
	mov byte ptr [rsi + ADDRx_H], dil

	xor r13, r13								; clear r13b, as we use this to detect if we need to call vera
	pop rsi
	ret

step_data1:
	mov rdi, [rdx].state.data1_address

	if write_value eq 1 and doublestep eq 0
		vera_write_dataport
	endif

	if write_value eq 0
		vera_read_dataport_cache
	endif

	add rdi, [rdx].state.data1_step
	and rdi, 1ffffh								; mask off high bits so we wrap
	;int 3

	mov r13d, [rdx].state.fx_addr_mode
	test r13d, r13d
	jz not_fx_1
	cmp r13d, 2
	je polyfill_1
	jg affine_1	

	; line helper mode, so stop on x
	mov r13d, [rdx].state.fx_x_position
	add r13d, [rdx].state.fx_x_increment
	
	test r13d, 00010000h
	jz no_fx_step_1
	
	and r13d, 0fffeffffh					; always mask out the increment bit
	mov [rdx].state.fx_x_position, r13d

	;step on data1 by data0's step
	add rdi, [rdx].state.data0_step
	and rdi, 1ffffh

	jmp not_fx_1

no_fx_step_1:
	mov [rdx].state.fx_x_position, r13d

	jmp not_fx_1
polyfill_1:
affine_1:

not_fx_1:
	if doublestep eq 1
		if write_value eq 1
			vera_write_dataport
		endif

		add rdi, [rdx].state.data1_step
		and rdi, 1ffffh							; mask off high bits so we wrap
	endif

	mov [rdx].state.data1_address, rdi

	mov r13b, byte ptr [rax+rdi]
	mov [rsi+rbx], r13b							; store in ram

	xor r13, r13								; clear r13b, as we use this to detect if we need to call vera

	cmp [rdx].state.addrsel, 1
	je set_data1_address
	pop rsi
	ret

set_data1_address:
		
	;mov rsi, [rdx].state.memory_ptr
	mov word ptr [rsi + ADDRx_L], di			; write L and M bytes

	;shr rdi, 8
	;mov byte ptr [rsi + ADDRx_M], dil

	shr rdi, 16									; need to add bottom bit of the address, so shift and mask
	and rdi, 1
	movzx rax, byte ptr [rsi + ADDRx_H]			; Add on stepping nibble
	and eax, 0f8h								; mask off what isnt changable
	or rdi, rax
	mov byte ptr [rsi + ADDRx_H], dil

	xor r13, r13								; clear r13b, as we use this to detect if we need to call vera
	pop rsi

	ret
endm

;
; VERA post read side effect macros
;

vera_afterread proc
	;dec r13%
	vera_dataaccess_body 0, 0
vera_afterread endp

; eg inc, asl
vera_afterreadwrite proc
	;dec r13
	vera_dataaccess_body 1, 1
vera_afterreadwrite endp

;
; VERA read side effects for FX
;
; rbx			address
; [rsi+rbx]		output location in main memory
vera_afterread_9f29 proc
	; if DCSel is 6, reading FX_ACCUM_RESET resets accumulator
	movzx r13, byte ptr [rdx].state.dcsel

	cmp r13d, 2
	jl done
	je dc_sel_2

	cmp r13d, 4
	jl dc_sel_3
	je dc_sel_4

	cmp r13d, 6
	jl dc_sel_5
	je dc_sel_6
	ret

dc_sel_2:
	ret
dc_sel_3:
	ret
dc_sel_4:
	ret
dc_sel_5:
	ret
dc_sel_6:
	mov [rdx].state.fx_accumulator, 0
	ret
done:
	ret
vera_afterread_9f29 endp

vera_afterread_9f2a proc
	; if DCSel is 6, reading FX_ACCUM sets accumulator
	movzx r13, byte ptr [rdx].state.dcsel

	cmp r13d, 6
	jne done

	; this latches the output of the multiplication and copies it to the accumulator
	mov r12d, [rdx].state.fx_cache			; get the cache value

	mov ecx, r12d
	shr ecx, 16
	movsx r12d, r12w
	movsx ecx, cx
	imul r12d, ecx

	mov ecx, [rdx].state.fx_accumulator
	lea r13, [r12 + rcx]					; addition
	sub ecx, r12d							; sub
	mov r12d, ecx

	test [rdx].state.fx_accumulate_direction, 1
	cmovz r12d, r13d						; is sub	

	mov [rdx].state.fx_accumulator, r12d

done:
	ret
vera_afterread_9f2a endp

;
; Update procs for vera registers
;

vera_update_notimplemented proc
	ret
vera_update_notimplemented endp

vera_update_data proc
	vera_dataaccess_body 0, 1
vera_update_data endp

; Update Data0 if the address changes
vera_update_data0 macro
	mov r13d, dword ptr [rdx].state.data0_address
	mov rdi, [rdx].state.vram_ptr
	mov r13b, byte ptr [rdi + r13]
	mov byte ptr [rsi+9f23h], r13b
endm

; Update Data1 if the address changes
vera_update_data1 macro
	mov r13d, dword ptr [rdx].state.data1_address
	mov rdi, [rdx].state.vram_ptr
	mov r13b, byte ptr [rdi + r13]
	mov byte ptr [rsi+9f24h], r13b
endm

vera_update_addrl proc	
	mov r13b, byte ptr [rsi+rbx]
	cmp byte ptr [rdx].state.addrsel, 0

	jnz write_data1
	mov byte ptr [rdx].state.data0_address, r13b
	vera_update_data0
	ret
write_data1:
	mov byte ptr [rdx].state.data1_address, r13b
	vera_update_data1
	ret
vera_update_addrl endp

vera_update_addrm proc	
	mov r13b, byte ptr [rsi+rbx]
	cmp byte ptr [rdx].state.addrsel, 0

	jnz write_data1
	mov byte ptr [rdx].state.data0_address + 1, r13b
	vera_update_data0
	ret
write_data1:
	mov byte ptr [rdx].state.data1_address + 1, r13b
	vera_update_data1
	ret
vera_update_addrm endp

vera_update_addrh proc	
	mov r13b, byte ptr [rsi+rbx]						; value that has been written
	and r13, 11111001b
	mov byte ptr [rsi+rbx], r13b						; write back masked value
	cmp byte ptr [rdx].state.addrsel, 0					; data 0 or 1?

	jnz write_data1

	; Top address bit
	xor r12, r12
	bt r13w, 0											; check bit 0, if set then set r12b and move to data address
	setc r12b
	mov byte ptr [rdx].state.data0_address + 2, r12b

	; Index
	mov r12, r13
	and r12, 11110000b									; mask off the index
	shr r12, 3											; index in the table, not 4 as its a word
	lea rax, vera_step_table
	mov r12w, word ptr [rax + r12]						; get value from table

	bt r13w, 3											; check DECR
	jnc no_decr_0
	neg r12
	
no_decr_0:
	mov qword ptr [rdx].state.data0_step, r12
	vera_update_data0
	ret

write_data1:
	; Top address bit
	xor r12, r12
	bt r13w, 0											; check bit 0, if set then set r12b and move to data address
	setc r12b
	mov byte ptr [rdx].state.data1_address + 2, r12b

	; Index
	mov r12, r13
	and r12, 11110000b									; mask off the index
	shr r12, 3											; index in the table, not 4 as its a word
	lea rax, vera_step_table
	mov r12w, word ptr [rax + r12]						; get value from table

	bt r13w, 3											; check DECR
	jnc no_decr_1
	neg r12
	
no_decr_1:
	mov qword ptr [rdx].state.data1_step, r12
	vera_update_data1
	ret
vera_update_addrh endp

vera_update_ctrl proc
	; todo: Handle reset
	mov r13b, byte ptr [rsi+rbx]						; value that has been written
	and r13b, 01111111b									; reset if write only
	mov byte ptr [rsi+rbx], r13b
	push r13

	; Addrsel
	xor r12, r12
	bt r13w, 0
	jc set_addr

	mov byte ptr [rdx].state.addrsel, 0
	vera_setaddress_0
	jmp addr_done

set_addr:
	mov byte ptr [rdx].state.addrsel, 1
	vera_setaddress_1

addr_done:
	pop r13

	shr r13d, 1

	mov byte ptr [rdx].state.dcsel, r13b

	lea rax, dc_sel_table
	add rax, [rax + r13 * 8]
	jmp rax

dc_sel0:
	xor r12, r12
	mov al, byte ptr [rdx].state.sprite_enable
	shl rax, 6
	mov r12b, byte ptr [rdx].state.layer1_enable
	shl r12b, 5
	or rax, r12
	mov r12b, byte ptr [rdx].state.layer0_enable
	shl r12b, 4
	or rax, r12

	mov r12d, dword ptr [rdx].state.video_output
	and r12, 0fh
	or rax, r12

	mov byte ptr [rsi+DC_VIDEO], al

	mov eax, dword ptr [rdx].state.dc_hscale
	shr rax, 9
	mov byte ptr [rsi+DC_HSCALE], al

	mov eax, dword ptr [rdx].state.dc_vscale
	shr rax, 9
	mov byte ptr [rsi+DC_VSCALE], al

	mov al, byte ptr [rdx].state.dc_border
	mov byte ptr [rsi+DC_BORDER], al

	ret
dc_sel1:
set_dcsel:
	mov byte ptr [rdx].state.dcsel, 1

	mov ax, word ptr [rdx].state.dc_hstart
	shr ax, 2
	mov byte ptr [rsi+DC_HSTART], al

	mov ax, word ptr [rdx].state.dc_hstop
	shr ax, 2
	mov byte ptr [rsi+DC_HSTOP], al

	mov ax, word ptr [rdx].state.dc_vstart
	shr ax, 1
	mov byte ptr [rsi+DC_VSTART], al

	mov ax, word ptr [rdx].state.dc_vstop
	shr ax, 1
	mov byte ptr [rsi+DC_VSTOP], al

	ret

dc_sel2:

	mov eax, [rdx].state.fx_addr_mode

	mov ebx, [rdx].state.fx_4bit_mode
	shl ebx, 2
	or eax, ebx
	
	mov ebx, [rdx].state.fx_one_byte_cycling
	shl ebx, 4
	or eax, ebx

	mov ebx, [rdx].state.fx_cache_fill
	shl ebx, 5
	or eax, ebx

	mov ebx, [rdx].state.fx_cache_write
	shl ebx, 6
	or eax, ebx

	mov ebx, [rdx].state.fx_transparancy
	shl ebx, 7
	or eax, ebx


	mov byte ptr [rsi+DC_FX_CTRL], al
	
	mov byte ptr [rsi+DC_VER1], VERA_VERSION_L
	mov byte ptr [rsi+DC_VER2], VERA_VERSION_M
	mov byte ptr [rsi+DC_VER3], VERA_VERSION_H

	ret

version:
not_supported:
	; default is the version

	mov byte ptr [rsi+DC_VER0], 056h
	mov byte ptr [rsi+DC_VER1], VERA_VERSION_L
	mov byte ptr [rsi+DC_VER2], VERA_VERSION_M
	mov byte ptr [rsi+DC_VER3], VERA_VERSION_H

	ret

dc_sel_table:
	qword dc_sel0 - dc_sel_table
	qword dc_sel1 - dc_sel_table
	qword dc_sel2 - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table

	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table

	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table

	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table

	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table

	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table

	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table

	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword not_supported - dc_sel_table
	qword version - dc_sel_table
vera_update_ctrl endp

vera_update_ien proc
	mov r13b, byte ptr [rsi+rbx]
	and r13b, 10001111b					; mask off unused bits
	mov byte ptr [rsi+rbx], r13b

	xor rax, rax
	bt r13, 0
	setc al
	mov byte ptr [rdx].state.interrupt_vsync, al
	
	xor rax, rax
	bt r13, 1
	setc al
	mov byte ptr [rdx].state.interrupt_line, al

	xor rax, rax
	bt r13, 2
	setc al
	mov byte ptr [rdx].state.interrupt_spcol, al

	xor rax, rax
	bt r13, 3
	setc al
	mov byte ptr [rdx].state.interrupt_aflow, al

	xor rax, rax
	bt r13, 7
	setc al
	mov byte ptr [rdx].state.interrupt_linenum + 1, al

	mov r13b, byte ptr [rdx].state.interrupt_spcol_hit
	shl r13, 1
	or r13b, byte ptr [rdx].state.interrupt_line_hit
	shl r13, 1
	or r13b, byte ptr [rdx].state.interrupt_vsync_hit

	xor rax, rax
	mov bl, byte ptr [rsi+IEN]
	and r13b, bl
	setnz al

	mov byte ptr [rdx].state.interrupt, al

	ret
vera_update_ien endp

vera_update_irqline_l proc
	mov r13b, byte ptr [rsi+rbx]
	mov byte ptr [rdx].state.interrupt_linenum, r13b

	ret
vera_update_irqline_l endp

; DC_VIDEO
vera_update_9f29 proc

	movzx r13, byte ptr [rdx].state.dcsel
	lea rax, dc_sel_table
	add rax, [rax + r13 * 8]
	jmp rax


	;movzx r13, byte ptr [rsi+rbx]
	;movzx rax, byte ptr [rdx].state.dcsel
	;cmp byte ptr [rdx].state.dcsel, 0
	;jnz dcsel_set

dc_sel0:
	movzx r13, byte ptr [rsi+rbx]
	and r13, 01111111b
	mov byte ptr [rsi+rbx], r13b

	xor rax, rax
	bt r13, 4
	setc al
	mov byte ptr [rdx].state.layer0_enable, al 

	xor rax, rax
	bt r13, 5
	setc al
	mov byte ptr [rdx].state.layer1_enable, al 

	xor rax, rax
	bt r13, 6
	setc al
	mov byte ptr [rdx].state.sprite_enable, al 

	and r13, 0fh
	mov dword ptr [rdx].state.video_output, r13d

	ret
dc_sel1:
	movzx r13, byte ptr [rsi+rbx]
	shl r13, 2
	mov word ptr [rdx].state.dc_hstart, r13w

	ret

dc_sel2:	

	movzx r13, byte ptr [rsi+rbx]

	mov eax, r13d
	and eax, 011b
	mov [rdx].state.fx_addr_mode, eax

	mov eax, r13d
	and eax, 010000b
	shr eax, 4
	mov [rdx].state.fx_one_byte_cycling, eax

	mov eax, r13d
	and eax, 0100000b
	shr eax, 5
	mov [rdx].state.fx_cache_fill, eax

	mov eax, r13d
	and eax, 01000000b
	shr eax, 6
	mov [rdx].state.fx_cache_write, eax	

	mov eax, r13d
	and eax, 010000000b
	shr eax, 7
	mov [rdx].state.fx_transparancy, eax	

	; out of order, so we can process the index
	mov eax, r13d
	and eax, 0100b
	shr eax, 2
	mov [rdx].state.fx_4bit_mode, eax
	
	; set cache index if based on 4bit mode
	test eax, 1
	jnz dc_sel2_4bit

dc_sel2_8bit:
	mov eax, [rdx].state.fx_cache_index
	shr eax, 1
	shl eax, 3
	mov [rdx].state.fx_cache_fill_shift, eax

	ret

dc_sel2_4bit:
	mov eax, [rdx].state.fx_cache_index
	shl eax, 2
	mov [rdx].state.fx_cache_fill_shift, eax

	ret

dc_sel3:
	mov r13b, byte ptr [rsi+rbx]
	; r13 is the bottom 8 bits of a 6.9 fixed point number, convert to 16.16
	shl r13d, 7
	mov ecx, [rdx].state.fx_x_increment
	and ecx, 0ffff8000h
	or ecx, r13d
	mov [rdx].state.fx_x_increment, ecx

	mov byte ptr [rsi+rbx], r12b
	ret

dc_sel6:
	movzx r13, byte ptr [rsi+rbx]
	mov eax, [rdx].state.fx_cache
	and eax, 0ffffff00h
	or eax, r13d
	mov [rdx].state.fx_cache, eax
		
	mov byte ptr [rsi+rbx], r12b
	ret

dc_notsupported:
	mov byte ptr [rsi+rbx], r12b
	ret

dc_sel_table:
	qword dc_sel0 - dc_sel_table
	qword dc_sel1 - dc_sel_table
	qword dc_sel2 - dc_sel_table
	qword dc_sel3 - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_sel6 - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

vera_update_9f29 endp

vera_update_9f2a proc
	movzx r13, byte ptr [rdx].state.dcsel
	lea rax, dc_sel_table
	add rax, [rax + r13 * 8]
	jmp rax

	;movzx r13, byte ptr [rsi+rbx]
	;cmp byte ptr [rdx].state.dcsel, 0
	;jnz dcsel_set

dc_sel0:
	movzx r13, byte ptr [rsi+rbx]
	shl r13, 9
	mov dword ptr [rdx].state.dc_hscale, r13d
	ret
dcsel_set:
dc_sel1:
	movzx r13, byte ptr [rsi+rbx]
	shl r13, 2
	mov word ptr [rdx].state.dc_hstop, r13w
	ret

dc_sel2:
	movzx r13, byte ptr [rsi+rbx]
	mov eax, r13d
	and r13b, 01111111b
	; r13 is the top 7 bits of a 6.9 fixed point number, convert to 16.16
	shl r13d, 7+8
	mov ecx, [rdx].state.fx_x_increment
	and ecx, 000007fffh
	or ecx, r13d
	mov [rdx].state.fx_x_increment, ecx

	shr eax, 7
	mov [rdx].state.fx_x_mult_32, eax

	mov eax, [rdx].state.fx_addr_mode
	test eax, eax
	jz dc_sel2_done

	cmp [rdx].state.fx_addr_mode, 2
	jl dc_sel_line_mode
	je dc_sel_poly_fill

	; affine helper
	ret

dc_sel_line_mode:
	mov eax, [rdx].state.fx_x_position
	and eax, 0fffe0000h						; clear bit '0', the overflow
	or eax, 00008000h
	mov [rdx].state.fx_x_position, eax
	ret

dc_sel_poly_fill:
	mov eax, [rdx].state.fx_x_position
	and eax, 0ffff0000h
	or eax, 00008000h
	mov [rdx].state.fx_x_position, eax
	
dc_sel2_done:
	ret

dc_sel6:
	movzx r13, byte ptr [rsi+rbx]
	mov eax, [rdx].state.fx_cache
	and eax, 0ffff00ffh
	shl r13, 8
	or eax, r13d
	mov [rdx].state.fx_cache, eax
		
	mov byte ptr [rsi+rbx], r12b
	ret

dc_notsupported:
	mov byte ptr [rsi+rbx], r12b
	ret

dc_sel_table:
	qword dc_sel0 - dc_sel_table
	qword dc_sel1 - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_sel2 - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_sel6 - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

vera_update_9f2a endp

vera_update_9f2b proc
	movzx r13, byte ptr [rdx].state.dcsel
	lea rax, dc_sel_table
	add rax, [rax + r13 * 8]
	jmp rax

	;movzx r13, byte ptr [rsi+rbx]
	;cmp byte ptr [rdx].state.dcsel, 0
	;jnz dcsel_set

dc_sel0:
	movzx r13, byte ptr [rsi+rbx]
	shl r13, 9
	mov dword ptr [rdx].state.dc_vscale, r13d
	ret
dc_sel1:
	movzx r13, byte ptr [rsi+rbx]
	shl r13, 1
	mov word ptr [rdx].state.dc_vstart, r13w
	ret

dc_sel6:
	movzx r13, byte ptr [rsi+rbx]
	mov eax, [rdx].state.fx_cache
	and eax, 0ff00ffffh
	shl r13, 16
	or eax, r13d
	mov [rdx].state.fx_cache, eax
		
	mov byte ptr [rsi+rbx], r12b
	ret

dc_notsupported:
	mov byte ptr [rsi+rbx], r12b
	ret

dc_sel_table:
	qword dc_sel0 - dc_sel_table
	qword dc_sel1 - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_sel6 - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table


vera_update_9f2b endp

vera_update_9f2c proc
	movzx r13, byte ptr [rdx].state.dcsel
	lea rax, dc_sel_table
	add rax, [rax + r13 * 8]
	jmp rax

	;movzx r13, byte ptr [rsi+rbx]
	;cmp byte ptr [rdx].state.dcsel, 0
	;jnz dcsel_set

dc_sel0:
	movzx r13, byte ptr [rsi+rbx]
	mov byte ptr [rdx].state.dc_border, r13b
	ret

dc_sel1:
	movzx r13, byte ptr [rsi+rbx]
	shl r13, 1
	mov word ptr [rdx].state.dc_vstop, r13w
	ret

dc_sel2:
	movzx r13, byte ptr [rsi+rbx]
	mov byte ptr [rsi+rbx], r12b	; write only

	mov eax, r13d
	and eax, 1
	mov [rdx].state.fx_2byte_cache_incr, eax

	mov eax, r13d
	and eax, 10h
	shr eax, 4
	mov [rdx].state.fx_multiplier_enable, eax

	mov eax, r13d
	shr eax, 1
	and eax, 0111b
	mov [rdx].state.fx_cache_index, eax

	; THIS IS BROKEN. We do not handle 4bit index movement properly AT ALL here
	mov ecx, [rdx].state.fx_4bit_mode	; 1 for 4bit mode
	xor ecx, 0fffffffeh					; invert, so we can and the new index to set the shift
	and eax, ecx
	shl eax, 2
	mov [rdx].state.fx_cache_fill_shift, eax
			
	mov eax, r13d
	and eax, 00100000b
	shr eax, 5
	mov [rdx].state.fx_accumulate_direction, eax

	test r13d, 01000000b
	jz no_latch

	push r13
	; this latches the output of the multiplication and copies it to the accumulator
	mov r12d, [rdx].state.fx_cache			; get the cache value

	mov ecx, r12d
	shr ecx, 16
	movsx r12d, r12w
	movsx ecx, cx
	imul r12d, ecx

	mov ecx, [rdx].state.fx_accumulator
	lea r13, [r12 + rcx]					; addition
	sub ecx, r12d							; sub
	mov r12d, ecx

	test [rdx].state.fx_accumulate_direction, 1
	cmovz r12d, r13d						; is sub	

	mov [rdx].state.fx_accumulator, r12d
	pop r13

no_latch:
;	shr eax, 6
;	mov [rdx].state.fx_accumulate, eax


	; Reset accumulator
	mov eax, [rdx].state.fx_accumulator
	xor ecx, ecx
	test r13d, 10000000b
	cmovnz eax, ecx
	mov [rdx].state.fx_accumulator, eax

	ret

;	test [rdx].state.fx_4bit_mode, 1
;	jnz dc_sel2_4bit

;dc_sel2_8bit:
;	shr eax, 1						; ignore nibble bit
;	shl eax, 3						; * 8
;	mov [rdx].state.fx_cache_fill_shift, eax

;	ret

;dc_sel2_4bit:
;	shl eax, 2						; * 4
;	mov [rdx].state.fx_cache_fill_shift, eax

;	ret

dc_sel6:
	movzx r13, byte ptr [rsi+rbx]
	mov byte ptr [rsi+rbx], r12b	; write only

	mov eax, [rdx].state.fx_cache
	and eax, 000ffffffh
	shl r13, 24
	or eax, r13d
	mov [rdx].state.fx_cache, eax
		
	ret

dc_notsupported:
	mov byte ptr [rsi+rbx], r12b
	ret

dc_sel_table:
	qword dc_sel0 - dc_sel_table
	qword dc_sel1 - dc_sel_table
	qword dc_sel2 - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_sel6 - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table
	qword dc_notsupported - dc_sel_table

vera_update_9f2c endp

;
; Layer 0 Config
;

vera_update_l0config proc
	movzx r13, byte ptr [rsi+rbx]
	lea rbx, vera_map_shift_table

	mov rax, r13
	and rax, 00000011b
	mov byte ptr [rdx].state.layer0_colourDepth, al

	mov rax, r13
	and rax, 00000100b
	shr rax, 2
	mov byte ptr [rdx].state.layer0_bitmapMode, al
	
	mov rax, r13
	and rax, 00001000b
	shr rax, 3
	mov dword ptr [rdx].state.layer0_t256c, eax

	mov rax, r13
	and rax, 00110000b
	shr rax, 4
	mov byte ptr [rdx].state.layer0_mapWidth, al

	mov ax, word ptr [rbx + rax * 2]
	mov word ptr [rdx].state.layer0_map_vshift, ax

	mov rax, r13
	and rax, 11000000b
	shr rax, 6
	mov byte ptr [rdx].state.layer0_mapHeight, al

	mov ax, [rbx + rax * 2]
	mov word ptr [rdx].state.layer0_map_hshift, ax

	and r13, 00001111b
	lea rbx, layer0_render_jump
	add rbx, qword ptr [rbx + r13 * 8]
	mov qword ptr [rdx].state.layer0_renderer, rbx

	;mov word ptr [rdx].state.layer0_config, r13w

	set_layer0_jump

	ret
vera_update_l0config endp

vera_update_l0mapbase proc
	movzx r13, byte ptr [rsi+rbx]

	shl r13, 9
	mov dword ptr [rdx].state.layer0_mapAddress, r13d

	ret
vera_update_l0mapbase endp

vera_update_l0tilebase proc
	movzx r13, byte ptr [rsi+rbx]
	lea rbx, vera_tile_shift_table

	mov rax, r13
	and rax, 00000001b
	mov byte ptr [rdx].state.layer0_tileWidth, al

	mov ax, word ptr [rbx + rax * 2]
	mov word ptr [rdx].state.layer0_tile_vshift, ax

	mov rax, r13
	and rax, 00000010b
	shr rax, 1
	mov byte ptr [rdx].state.layer0_tileHeight, al

	mov ax, word ptr [rbx + rax * 2]
	mov word ptr [rdx].state.layer0_tile_hshift, ax

	and r13, 11111100b
	shl r13, 9											; not 11, as we're shifted by 2 bits already
	mov dword ptr [rdx].state.layer0_tileAddress, r13d

	set_layer0_jump

	ret
vera_update_l0tilebase endp

vera_update_l0hscroll_l proc
	mov r13b, byte ptr [rsi+rbx]
	mov byte ptr [rdx].state.layer0_hscroll, r13b

	ret
vera_update_l0hscroll_l endp

vera_update_l0hscroll_h proc
	mov r13b, byte ptr [rsi+rbx]
	and r13b, 0fh
	mov byte ptr [rsi+rbx], r13b
	mov byte ptr [rdx].state.layer0_hscroll + 1, r13b

	ret
vera_update_l0hscroll_h endp

vera_update_l0vscroll_l proc
	mov r13b, byte ptr [rsi+rbx]
	mov byte ptr [rdx].state.layer0_vscroll, r13b

	ret
vera_update_l0vscroll_l endp

vera_update_l0vscroll_h proc
	mov r13b, byte ptr [rsi+rbx]
	and r13b, 0fh
	mov byte ptr [rsi+rbx], r13b
	mov byte ptr [rdx].state.layer0_vscroll + 1, r13b

	ret
vera_update_l0vscroll_h endp
;
; Layer 1 Config
;

vera_update_l1config proc
	movzx r13, byte ptr [rsi+rbx]
	lea rbx, vera_map_shift_table

	mov rax, r13
	and rax, 00000011b
	mov byte ptr [rdx].state.layer1_colourDepth, al

	mov rax, r13
	and rax, 00000100b
	shr rax, 2
	mov byte ptr [rdx].state.layer1_bitmapMode, al

	mov rax, r13
	and rax, 00001000b
	shr rax, 3
	mov dword ptr [rdx].state.layer1_t256c, eax

	mov rax, r13
	and rax, 00110000b
	shr rax, 4
	mov byte ptr [rdx].state.layer1_mapWidth, al
	mov ax, [rbx + rax * 2]
	mov word ptr [rdx].state.layer1_map_vshift, ax

	mov rax, r13
	and rax, 11000000b
	shr rax, 6
	mov byte ptr [rdx].state.layer1_mapHeight, al
	
	mov ax, [rbx + rax * 2]
	mov word ptr [rdx].state.layer1_map_hshift, ax

	and r13, 00001111b
	lea rbx, layer1_render_jump
	add rbx, qword ptr [rbx + r13 * 8]
	mov qword ptr [rdx].state.layer1_renderer, rbx

	;mov word ptr [rdx].state.layer1_config, r13w

	set_layer1_jump
	
	ret
vera_update_l1config endp

vera_update_l1mapbase proc
	movzx r13, byte ptr [rsi+rbx]

	shl r13, 9
	mov dword ptr [rdx].state.layer1_mapAddress, r13d

	ret
vera_update_l1mapbase endp

vera_update_l1tilebase proc
	movzx r13, byte ptr [rsi+rbx]
	lea rbx, vera_tile_shift_table

	mov rax, r13
	and rax, 00000001b
	mov byte ptr [rdx].state.layer1_tileWidth, al

	mov ax, word ptr [rbx + rax * 2]
	mov word ptr [rdx].state.layer1_tile_vshift, ax


	mov rax, r13
	and rax, 00000010b
	shr rax, 1
	mov byte ptr [rdx].state.layer1_tileHeight, al

	mov ax, word ptr [rbx + rax * 2]
	mov word ptr [rdx].state.layer1_tile_hshift, ax


	and r13, 11111100b
	shl r13, 9											; not 11, as we're shifted by 2 bits already
	mov dword ptr [rdx].state.layer1_tileAddress, r13d

	set_layer1_jump

	ret
vera_update_l1tilebase endp

vera_update_l1hscroll_l proc
	mov r13b, byte ptr [rsi+rbx]
	mov byte ptr [rdx].state.layer1_hscroll, r13b

	ret
vera_update_l1hscroll_l endp

vera_update_l1hscroll_h proc
	mov r13b, byte ptr [rsi+rbx]
	and r13b, 0fh
	mov byte ptr [rsi+rbx], r13b
	mov byte ptr [rdx].state.layer1_hscroll + 1, r13b

	ret
vera_update_l1hscroll_h endp

vera_update_l1vscroll_l proc
	mov r13b, byte ptr [rsi+rbx]
	mov byte ptr [rdx].state.layer1_vscroll, r13b

	ret
vera_update_l1vscroll_l endp

vera_update_l1vscroll_h proc
	mov r13b, byte ptr [rsi+rbx]
	and r13b, 0fh
	mov byte ptr [rsi+rbx], r13b
	mov byte ptr [rdx].state.layer1_vscroll + 1, r13b

	ret
vera_update_l1vscroll_h endp

vera_update_isr proc
	mov r13b, byte ptr [rsi+rbx]
	and r12, 0f8h				; mask out lower three bits from previous value

	bt r13, 0
	jnc check_line

	mov [rdx].state.interrupt_vsync_hit, 0

check_line:
	bt r13, 1
	jnc check_spcol

	mov [rdx].state.interrupt_line_hit, 0

check_spcol:
	bt r13, 2
	jnc construct_isr

	mov [rdx].state.interrupt_spcol_hit, 0

construct_isr:
	mov r13b, byte ptr [rdx].state.interrupt_spcol_hit
	shl r13, 1
	or r13b, byte ptr [rdx].state.interrupt_line_hit
	shl r13, 1
	or r13b, byte ptr [rdx].state.interrupt_vsync_hit
	or r13, r12						; or back on the top 5 bits.
	mov byte ptr [rsi+rbx], r13b

	xor rax, rax
	mov bl, byte ptr [rsi+IEN]
	and ebx, 0fh					; only consider bottom 4 bits.
	and r13b, bl

	setnz al

	mov byte ptr [rdx].state.interrupt, al

	ret
vera_update_isr endp

vera_update_audiorate proc
	movzx r13, byte ptr [rsi+rbx]
	mov [rdx].state.pcm_samplerate, r13d

	ret
vera_update_audiorate endp

vera_update_audiodata proc
	mov eax, [rdx].state.pcm_bufferwrite
	mov ecx, eax
	inc eax
	and eax, 0fffh	; constrain to 4k
	cmp eax, [rdx].state.pcm_bufferread
	je full

	movzx r13, byte ptr [rsi+rbx]	

	mov rbx, [rdx].state.pcm_ptr
	mov [rdx].state.pcm_bufferwrite, eax

	mov byte ptr[rbx + rcx], r13b			; write to the original write index

	; Todo: the check for empty and aflow could be merged.
	; check if fifo is empty now so we can set the AUDIO_CTRL bit!
	xor ebx, ebx
	mov r13d, 080h							; buffer full bit
	inc eax
	and eax, 0fffh
	cmp eax, [rdx].state.pcm_bufferread

	cmovne r13d, ebx						; clear buffer full bit

	and byte ptr [rsi + AUDIO_CTRL], 03fh	; clear empty bit
	or byte ptr [rsi + AUDIO_CTRL], r13b	; set buffer full if necessary

	; AFLOW
	mov eax, [rdx].state.pcm_bufferwrite
	sub eax, [rdx].state.pcm_bufferread
	xor ebx, ebx
	mov rcx, 01000b							; AFLOW flag
	cmp eax, 0400h
	cmova ecx, ebx							; clear if greater
	movzx rbx, byte ptr [rsi+ISR]
	and ebx, 011110111b						; clear
	or ebx, ecx								; set if neccesary
	mov byte ptr [rsi+ISR], bl				; write
	shr ecx, 4
	or byte ptr [rdx].state.interrupt, cl

	ret
full:
	or byte ptr [rsi + AUDIO_CTRL], 080h	; set full bit

	ret

vera_update_audiodata endp

vera_update_audioctrl proc
	movzx r13, byte ptr [rsi+rbx]

	; handle reset
	bt r13, 7
	jnc no_reset
	xor ebx, ebx
	mov [rdx].state.pcm_bufferwrite, ebx
	mov [rdx].state.pcm_bufferread, ebx

	or byte ptr [rsi + AUDIO_CTRL], 040h	; set empty flag

	xor ecx, ecx
	movzx rbx, byte ptr [rsi + ISR]			
	or ebx, 1000b							; set aflow
	mov byte ptr [rsi + ISR], bl
	and bl, byte ptr [rsi + IEN]
	and ebx, 1000b
	shr ebx, 4

	or byte ptr [rdx].state.interrupt, bl	; or on the interrupt

no_reset:
	; set 16bit / stereo
	mov rbx, r13
	and rbx, 030h
	shr rbx, 4
	mov [rdx].state.pcm_mode, ebx

	and r13, 0fh
	lea rbx, pcm_volume
	mov r13d, dword ptr [rbx + r13 * 4]
	mov [rdx].state.pcm_volume, r13d

	ret
vera_update_audioctrl endp


vera_step_table:
	dw 0, 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 40, 80, 160, 320, 640

vera_tile_shift_table:
	dw 3, 4
vera_map_shift_table:
	dw 5, 6, 7, 8

align 8
data_cache_mask:
	dword 0ffffffffh
	dword 0fffffff0h
	dword 0ffffff0fh
	dword 0ffffff00h
	dword 0fffff0ffh
	dword 0fffff0f0h
	dword 0fffff00fh
	dword 0fffff000h
	dword 0ffff0fffh
	dword 0ffff0ff0h
	dword 0ffff0f0fh
	dword 0ffff0f00h
	dword 0ffff00ffh
	dword 0ffff00f0h
	dword 0ffff000fh
	dword 0ffff0000h
	dword 0fff0ffffh
	dword 0fff0fff0h
	dword 0fff0ff0fh
	dword 0fff0ff00h
	dword 0fff0f0ffh
	dword 0fff0f0f0h
	dword 0fff0f00fh
	dword 0fff0f000h
	dword 0fff00fffh
	dword 0fff00ff0h
	dword 0fff00f0fh
	dword 0fff00f00h
	dword 0fff000ffh
	dword 0fff000f0h
	dword 0fff0000fh
	dword 0fff00000h
	dword 0ff0fffffh
	dword 0ff0ffff0h
	dword 0ff0fff0fh
	dword 0ff0fff00h
	dword 0ff0ff0ffh
	dword 0ff0ff0f0h
	dword 0ff0ff00fh
	dword 0ff0ff000h
	dword 0ff0f0fffh
	dword 0ff0f0ff0h
	dword 0ff0f0f0fh
	dword 0ff0f0f00h
	dword 0ff0f00ffh
	dword 0ff0f00f0h
	dword 0ff0f000fh
	dword 0ff0f0000h
	dword 0ff00ffffh
	dword 0ff00fff0h
	dword 0ff00ff0fh
	dword 0ff00ff00h
	dword 0ff00f0ffh
	dword 0ff00f0f0h
	dword 0ff00f00fh
	dword 0ff00f000h
	dword 0ff000fffh
	dword 0ff000ff0h
	dword 0ff000f0fh
	dword 0ff000f00h
	dword 0ff0000ffh
	dword 0ff0000f0h
	dword 0ff00000fh
	dword 0ff000000h
	dword 0f0ffffffh
	dword 0f0fffff0h
	dword 0f0ffff0fh
	dword 0f0ffff00h
	dword 0f0fff0ffh
	dword 0f0fff0f0h
	dword 0f0fff00fh
	dword 0f0fff000h
	dword 0f0ff0fffh
	dword 0f0ff0ff0h
	dword 0f0ff0f0fh
	dword 0f0ff0f00h
	dword 0f0ff00ffh
	dword 0f0ff00f0h
	dword 0f0ff000fh
	dword 0f0ff0000h
	dword 0f0f0ffffh
	dword 0f0f0fff0h
	dword 0f0f0ff0fh
	dword 0f0f0ff00h
	dword 0f0f0f0ffh
	dword 0f0f0f0f0h
	dword 0f0f0f00fh
	dword 0f0f0f000h
	dword 0f0f00fffh
	dword 0f0f00ff0h
	dword 0f0f00f0fh
	dword 0f0f00f00h
	dword 0f0f000ffh
	dword 0f0f000f0h
	dword 0f0f0000fh
	dword 0f0f00000h
	dword 0f00fffffh
	dword 0f00ffff0h
	dword 0f00fff0fh
	dword 0f00fff00h
	dword 0f00ff0ffh
	dword 0f00ff0f0h
	dword 0f00ff00fh
	dword 0f00ff000h
	dword 0f00f0fffh
	dword 0f00f0ff0h
	dword 0f00f0f0fh
	dword 0f00f0f00h
	dword 0f00f00ffh
	dword 0f00f00f0h
	dword 0f00f000fh
	dword 0f00f0000h
	dword 0f000ffffh
	dword 0f000fff0h
	dword 0f000ff0fh
	dword 0f000ff00h
	dword 0f000f0ffh
	dword 0f000f0f0h
	dword 0f000f00fh
	dword 0f000f000h
	dword 0f0000fffh
	dword 0f0000ff0h
	dword 0f0000f0fh
	dword 0f0000f00h
	dword 0f00000ffh
	dword 0f00000f0h
	dword 0f000000fh
	dword 0f0000000h
	dword 00fffffffh
	dword 00ffffff0h
	dword 00fffff0fh
	dword 00fffff00h
	dword 00ffff0ffh
	dword 00ffff0f0h
	dword 00ffff00fh
	dword 00ffff000h
	dword 00fff0fffh
	dword 00fff0ff0h
	dword 00fff0f0fh
	dword 00fff0f00h
	dword 00fff00ffh
	dword 00fff00f0h
	dword 00fff000fh
	dword 00fff0000h
	dword 00ff0ffffh
	dword 00ff0fff0h
	dword 00ff0ff0fh
	dword 00ff0ff00h
	dword 00ff0f0ffh
	dword 00ff0f0f0h
	dword 00ff0f00fh
	dword 00ff0f000h
	dword 00ff00fffh
	dword 00ff00ff0h
	dword 00ff00f0fh
	dword 00ff00f00h
	dword 00ff000ffh
	dword 00ff000f0h
	dword 00ff0000fh
	dword 00ff00000h
	dword 00f0fffffh
	dword 00f0ffff0h
	dword 00f0fff0fh
	dword 00f0fff00h
	dword 00f0ff0ffh
	dword 00f0ff0f0h
	dword 00f0ff00fh
	dword 00f0ff000h
	dword 00f0f0fffh
	dword 00f0f0ff0h
	dword 00f0f0f0fh
	dword 00f0f0f00h
	dword 00f0f00ffh
	dword 00f0f00f0h
	dword 00f0f000fh
	dword 00f0f0000h
	dword 00f00ffffh
	dword 00f00fff0h
	dword 00f00ff0fh
	dword 00f00ff00h
	dword 00f00f0ffh
	dword 00f00f0f0h
	dword 00f00f00fh
	dword 00f00f000h
	dword 00f000fffh
	dword 00f000ff0h
	dword 00f000f0fh
	dword 00f000f00h
	dword 00f0000ffh
	dword 00f0000f0h
	dword 00f00000fh
	dword 00f000000h
	dword 000ffffffh
	dword 000fffff0h
	dword 000ffff0fh
	dword 000ffff00h
	dword 000fff0ffh
	dword 000fff0f0h
	dword 000fff00fh
	dword 000fff000h
	dword 000ff0fffh
	dword 000ff0ff0h
	dword 000ff0f0fh
	dword 000ff0f00h
	dword 000ff00ffh
	dword 000ff00f0h
	dword 000ff000fh
	dword 000ff0000h
	dword 000f0ffffh
	dword 000f0fff0h
	dword 000f0ff0fh
	dword 000f0ff00h
	dword 000f0f0ffh
	dword 000f0f0f0h
	dword 000f0f00fh
	dword 000f0f000h
	dword 000f00fffh
	dword 000f00ff0h
	dword 000f00f0fh
	dword 000f00f00h
	dword 000f000ffh
	dword 000f000f0h
	dword 000f0000fh
	dword 000f00000h
	dword 0000fffffh
	dword 0000ffff0h
	dword 0000fff0fh
	dword 0000fff00h
	dword 0000ff0ffh
	dword 0000ff0f0h
	dword 0000ff00fh
	dword 0000ff000h
	dword 0000f0fffh
	dword 0000f0ff0h
	dword 0000f0f0fh
	dword 0000f0f00h
	dword 0000f00ffh
	dword 0000f00f0h
	dword 0000f000fh
	dword 0000f0000h
	dword 00000ffffh
	dword 00000fff0h
	dword 00000ff0fh
	dword 00000ff00h
	dword 00000f0ffh
	dword 00000f0f0h
	dword 00000f00fh
	dword 00000f000h
	dword 000000fffh
	dword 000000ff0h
	dword 000000f0fh
	dword 000000f00h
	dword 0000000ffh
	dword 0000000f0h
	dword 00000000fh
	dword 000000000h

.code
