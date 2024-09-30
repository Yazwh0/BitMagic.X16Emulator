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


render_layers_to_buffer proc
;
; RENDERING TO BUFFER
;
; Needs act x, scaled y + 1 line from VIDEO
;

	; set r12 to scaled y 
	mov r12d, dword ptr [rdx].state.scale_y
	shr r12, 16							; adjust to actual value

	;
	; Render next lines
	;

	;
	; Layer 0
	;

	mov eax, [rdx].state.layer0_wait
	test eax, eax
	jnz layer0_waiting

	mov r11d, dword ptr [rdx].state.layer0_x
	cmp r11d, 640			; optimised check, todo: actual limit is in renderer from buffer
	jge layer0_done
	
	push r12
	push r15

	and r15d, 800h ;01000 0000 0000b			; set r15\buffer position to be just the offset
	or r15d, r11d
	
	; check render state
	mov eax, dword ptr [rdx].state.layer0_state
	lea rbx, state0_jump_table
	add rbx, [rbx + rax * 8]
	jmp rbx

state0_jump_table:
	qword handle0_wait - state0_jump_table	; skip is simply no longer rendering as the line is complete
	qword handle0_fetch_map - state0_jump_table
	qword handle0_fetch_map_wait - state0_jump_table
	qword handle0_fetch_tile - state0_jump_table
	qword handle0_fetch_tile_wait - state0_jump_table
	qword handle0_render_tile - state0_jump_table
	qword handle0_fetch_bitmap - state0_jump_table
	qword handle0_fetch_bitmap_wait - state0_jump_table
	qword handle0_render_bitmap - state0_jump_table

handle0_wait:
	jmp layer0_complete

handle0_fetch_map:
	mov ebx, dword ptr [rdx].state.vram_wait	; cant render anything while waiting on the bus.
	test ebx, ebx
	jnz layer0_complete

	add r12w, word ptr [rdx].state.layer0_vscroll
	add r11w, word ptr [rdx].state.layer0_hscroll
	mov r13d, dword ptr [rdx].state.layer0_mapAddress
	call [rdx].state.layer0_fetchmap
	mov [rdx].state.layer0_mapdata, eax
	mov [rdx].state.layer0_tilecount, ebx
	mov [rdx].state.layer0_state, STATE_FETCH_MAP_WAIT
	jmp layer0_complete

handle0_fetch_map_wait: ; todo: move reads to here
	mov [rdx].state.layer0_state, STATE_FETCH_TILE
	mov [rdx].state.layer0_wait, 1
	jmp layer0_complete

handle0_fetch_tile:
	mov ebx, dword ptr [rdx].state.vram_wait	; cant render anything while waiting on the bus.
	test ebx, ebx
	jnz layer0_complete

	mov eax, [rdx].state.layer0_mapdata
	add r12w, word ptr [rdx].state.layer0_vscroll
	add r11w, word ptr [rdx].state.layer0_hscroll
	mov r14d, dword ptr [rdx].state.layer0_tileAddress
	call [rdx].state.layer0_fetchtile
	mov [rdx].state.layer0_tiledata, ebx
	mov [rdx].state.layer0_tiledone, r8d
	mov [rdx].state.layer0_tilepos, r10d
	mov [rdx].state.layer0_width, r13d
	mov [rdx].state.layer0_mask, r14d
	mov [rdx].state.layer0_state, STATE_FETCH_TILE_WAIT
	jmp layer0_complete

handle0_fetch_tile_wait: ; todo: move reads to here
	mov r15d, STATE_RENDER_TILE
	mov r12d, STATE_RENDER_BITMAP
	movzx rax, byte ptr [rdx].state.layer0_bitmapMode
	test rax, rax
	cmovnz r15d, r12d
	mov [rdx].state.layer0_state, r15d

	mov [rdx].state.layer0_wait, 1
	jmp layer0_complete

handle0_render_tile:
	mov eax, [rdx].state.layer0_mapdata
	and eax, 0ffffh
	mov ebx, [rdx].state.layer0_tiledata
	mov r10d, [rdx].state.layer0_tilepos
	mov r13d, [rdx].state.layer0_width
	mov r14d, [rdx].state.layer0_mask
	call [rdx].state.layer0_renderer

	add dword ptr [rdx].state.layer0_x, eax

	; check if any more px are left
	cmp [rdx].state.layer0_tiledone, -1
	je handle0_render_tilecomplete

	mov [rdx].state.layer0_state, STATE_FETCH_TILE
	jmp layer0_complete

handle0_render_tilecomplete:

	mov eax, [rdx].state.layer0_tilecount
	test eax, eax
	jz handle0_postrender_set_fetchmap

	; in tile mode, the read of the tile data is 32bits, so is two tiles per vram read
	shr [rdx].state.layer0_mapdata, 16
	mov [rdx].state.layer0_tilecount, 0

	mov [rdx].state.layer0_state, STATE_FETCH_TILE

	jmp layer0_complete

handle0_postrender_set_fetchmap:
	
	mov r15d, STATE_FETCH_MAP
	mov r12d, STATE_FETCH_BITMAP
	movzx rax, [rdx].state.layer0_bitmapMode
	test rax, rax
	cmovnz r15d, r12d
	mov [rdx].state.layer0_state, r15d

	jmp layer0_complete

handle0_fetch_bitmap:
	mov ebx, dword ptr [rdx].state.vram_wait	; cant render anything while waiting on the bus.
	test ebx, ebx
	jnz layer0_complete

	mov eax, [rdx].state.layer0_mapdata
	mov r14d, dword ptr [rdx].state.layer0_tileAddress
	call [rdx].state.layer0_fetchtile
	mov [rdx].state.layer0_tiledata, ebx
	mov [rdx].state.layer0_state, STATE_FETCH_BITMAP_WAIT
	jmp layer0_complete

handle0_fetch_bitmap_wait:
	mov [rdx].state.layer0_state, STATE_RENDER_BITMAP
	mov [rdx].state.layer0_wait, 1
	jmp layer0_complete

handle0_render_bitmap:
	;mov ebx, [rdx].state.layer0_tiledata

	mov eax, [rdx].state.layer0_mapdata
	and eax, 0ffffh
	mov ebx, [rdx].state.layer0_tiledata
	mov r10d, [rdx].state.layer0_tilepos
	mov r13d, [rdx].state.layer0_width
	mov r14d, [rdx].state.layer0_mask

	call [rdx].state.layer0_renderer

	add dword ptr [rdx].state.layer0_x, eax

	mov r15d, STATE_FETCH_MAP
	mov r12d, STATE_FETCH_BITMAP
	movzx rax, [rdx].state.layer0_bitmapMode
	test rax, rax
	cmovnz r15d, r12d
	mov [rdx].state.layer0_state, r15d

	jmp layer0_complete

layer0_waiting:
	dec eax
	mov [rdx].state.layer0_wait, eax
	jmp layer0_done
	
layer0_complete:
	pop r15
	pop r12

layer0_skip:
layer0_done:
	


	;
	; Layer 1
	;
	mov eax, [rdx].state.layer1_wait
	test eax, eax
	jnz layer1_waiting
		
	mov r11d, dword ptr [rdx].state.layer1_x
	cmp r11d, 640
	jge layer1_done

	push r12
	push r15

	and r15d, 800h		; set r15\buffer position to be just the offset
	or r15d, r11d

	; check render state
	mov eax, dword ptr [rdx].state.layer1_state
	lea rbx, state1_jump_table
	add rbx, [rbx + rax * 8]
	jmp rbx

state1_jump_table:
	qword handle1_wait - state1_jump_table	; skip is simply no longer rendering as the line is complete
	qword handle1_fetch_map - state1_jump_table
	qword handle1_fetch_map_wait - state1_jump_table
	qword handle1_fetch_tile - state1_jump_table
	qword handle1_fetch_tile_wait - state1_jump_table
	qword handle1_render_tile - state1_jump_table
	qword handle1_fetch_bitmap - state1_jump_table
	qword handle1_fetch_bitmap_wait - state1_jump_table
	qword handle1_render_bitmap - state1_jump_table
handle1_wait:
	jmp layer1_complete

handle1_fetch_map:
	mov ebx, dword ptr [rdx].state.vram_wait	; cant render anything while waiting on the bus.
	test ebx, ebx
	jnz layer1_complete

	add r12w, word ptr [rdx].state.layer1_vscroll
	add r11w, word ptr [rdx].state.layer1_hscroll
	mov r13d, dword ptr [rdx].state.layer1_mapAddress
	call [rdx].state.layer1_fetchmap
	mov [rdx].state.layer1_mapdata, eax
	mov [rdx].state.layer1_tilecount, ebx
	mov [rdx].state.layer1_state, STATE_FETCH_MAP_WAIT
	jmp layer1_complete

handle1_fetch_map_wait: ; todo: move reads to here
	mov [rdx].state.layer1_wait, 1
	mov [rdx].state.layer1_state, STATE_FETCH_TILE
	jmp layer1_complete

handle1_fetch_tile:
	mov ebx, dword ptr [rdx].state.vram_wait	; cant render anything while waiting on the bus.
	test ebx, ebx
	jnz layer1_complete

	mov eax, [rdx].state.layer1_mapdata
	add r12w, word ptr [rdx].state.layer1_vscroll
	add r11w, word ptr [rdx].state.layer1_hscroll
	mov r14d, dword ptr [rdx].state.layer1_tileAddress
	call [rdx].state.layer1_fetchtile
	mov [rdx].state.layer1_tiledata, ebx
	mov [rdx].state.layer1_tiledone, r8d
	mov [rdx].state.layer1_tilepos, r10d
	mov [rdx].state.layer1_width, r13d
	mov [rdx].state.layer1_mask, r14d
	mov [rdx].state.layer1_state, STATE_FETCH_TILE_WAIT
	jmp layer1_complete

handle1_fetch_tile_wait: ; todo: move reads to here
	mov r15d, STATE_RENDER_TILE
	mov r12d, STATE_RENDER_BITMAP
	movzx rax, byte ptr [rdx].state.layer1_bitmapMode
	test rax, rax
	cmovnz r15d, r12d
	mov [rdx].state.layer1_state, r15d

	mov [rdx].state.layer1_wait, 1
	jmp layer1_complete

handle1_render_tile:
	mov eax, [rdx].state.layer1_mapdata
	and eax, 0ffffh
	mov ebx, [rdx].state.layer1_tiledata
	mov r10d, [rdx].state.layer1_tilepos
	mov r13d, [rdx].state.layer1_width
	mov r14d, [rdx].state.layer1_mask
	call [rdx].state.layer1_renderer
	add dword ptr [rdx].state.layer1_x, eax

	; check if any more px are left
	cmp [rdx].state.layer1_tiledone, -1
	je handle1_render_tilecomplete

	mov [rdx].state.layer1_state, STATE_FETCH_TILE
	jmp layer1_complete

handle1_render_tilecomplete:

	mov eax, [rdx].state.layer1_tilecount
	test eax, eax
	jz handle1_postrender_set_fetchmap

	; in tile mode, the read of the tile data is 32bits, so is two tiles per vram read
	shr [rdx].state.layer1_mapdata, 16
	mov [rdx].state.layer1_tilecount, 0

	mov [rdx].state.layer1_state, STATE_FETCH_TILE
	
	jmp layer1_complete

handle1_postrender_set_fetchmap:

	mov r15d, STATE_FETCH_MAP
	mov r12d, STATE_FETCH_BITMAP
	movzx rax, [rdx].state.layer1_bitmapMode
	test rax, rax
	cmovnz r15d, r12d
	mov [rdx].state.layer1_state, r15d

	jmp layer1_complete

handle1_fetch_bitmap:
	mov ebx, dword ptr [rdx].state.vram_wait	; cant render anything while waiting on the bus.
	test ebx, ebx
	jnz layer1_complete

	mov eax, [rdx].state.layer1_mapdata
	mov r14d, dword ptr [rdx].state.layer1_tileAddress
	call [rdx].state.layer1_fetchtile
	mov [rdx].state.layer1_tiledata, ebx
	mov [rdx].state.layer1_state, STATE_FETCH_BITMAP_WAIT
	jmp layer1_complete

handle1_fetch_bitmap_wait:
	mov [rdx].state.layer1_state, STATE_RENDER_BITMAP
	mov [rdx].state.layer1_wait, 1
	jmp layer1_complete

handle1_render_bitmap:
	;mov ebx, [rdx].state.layer1_tiledata

	mov eax, [rdx].state.layer1_mapdata
	and eax, 0ffffh
	mov ebx, [rdx].state.layer1_tiledata
	mov r10d, [rdx].state.layer1_tilepos
	mov r13d, [rdx].state.layer1_width
	mov r14d, [rdx].state.layer1_mask

	call [rdx].state.layer1_renderer

	add dword ptr [rdx].state.layer1_x, eax

	mov r15d, STATE_FETCH_MAP
	mov r12d, STATE_FETCH_BITMAP
	movzx rax, [rdx].state.layer1_bitmapMode
	test rax, rax
	cmovnz r15d, r12d
	mov [rdx].state.layer1_state, r15d

	jmp layer1_complete

layer1_waiting:
	dec eax
	mov [rdx].state.layer1_wait, eax
	jmp layer1_done

layer1_complete:
	pop r15
	pop r12

layer1_done:

render_complete_visible:
	ret
render_layers_to_buffer endp