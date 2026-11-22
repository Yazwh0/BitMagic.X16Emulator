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

VERA_BREAKPOINT_READ equ  0001b
VERA_BREAKPOINT_WRITE equ 0010b
VERA_BREAKPOINT_LOAD equ  0100b

; Names for one data port's state, so the macros below are written once for both ports.
vera_port_fields macro port
    if port eq 0
        port_address textequ <[rdx].state.data0_address>
        port_step textequ <[rdx].state.data0_step>
        port_nibble textequ <[rdx].state.data0_nibble>
        port_nibble_incr textequ <[rdx].state.data0_nibble_incr>
        port_nibble_step textequ <[rdx].state.data0_nibble_step>
    else
        port_address textequ <[rdx].state.data1_address>
        port_step textequ <[rdx].state.data1_step>
        port_nibble textequ <[rdx].state.data1_nibble>
        port_nibble_incr textequ <[rdx].state.data1_nibble_incr>
        port_nibble_step textequ <[rdx].state.data1_nibble_step>
    endif
endm

; The nibble increment only applies in 4 bit mode with an increment of 0. Worked out here, when ADDRx_H or FX_CTRL is
; written, so stepping only has to check port_nibble_step. Uses eax.
vera_update_nibble_step macro port
    local done
    vera_port_fields port

    xor eax, eax
    cmp qword ptr port_step, 0
    jne done
    test dword ptr [rdx].state.fx_4bit_mode, 1
    jz done
    mov eax, port_nibble_incr
done:
    mov port_nibble_step, eax
endm

; Flips a nibble address in direction's direction (1 forwards, -1 backwards, 0 does nothing), moving rdi on a byte
; going from nibble 1 to 0, or back a byte going from 0 to 1.
vera_nibble_step macro nibble, direction
    local done, backwards

    cmp dword ptr direction, 0
    je done									; the usual case
    jl backwards

    xor dword ptr nibble, 1
    jnz done								; was nibble 0
    inc edi
    and edi, 1ffffh
    jmp done

backwards:
    xor dword ptr nibble, 1
    jz done									; was nibble 1
    dec edi
    and edi, 1ffffh
done:
endm

; Steps rdi by the port's own increment. With a nibble increment the nibble steps instead. Updates the port's nibble.
; ADDR1 with 16-bit hop steps +1 from its start alignment, then +3 (or +319), so it reads 16-bit values. Uses r13.
vera_port_step macro port
    local hop, hop_start, done
    vera_port_fields port

    add rdi, port_step
    and rdi, 1ffffh
    if port eq 1
        cmp dword ptr [rdx].state.fx_hop_active, 0
        jne hop
    endif
    vera_nibble_step port_nibble, port_nibble_step

    if port eq 1
        jmp done

    hop:
        mov r13, rdi
        sub r13, port_step						; where it was
        and r13d, 3
        cmp r13d, [rdx].state.fx_hop_start
        je hop_start
        dec rdi									; +3 or +319
        and rdi, 1ffffh
        jmp done
    hop_start:
        sub rdi, port_step						; +1
        inc rdi
        and rdi, 1ffffh
    done:
    endif
endm

; The line helper adds ADDR0's increment to rdi (ADDR1) when x passes a whole pixel.
;
; Hardware (the VERA Verilog, addr_data.v): ADDR1's nibble isn't changed. With a nibble increment on ADDR0 it adds what
; ADDR0 would step by: 1 forwards from nibble 1, -1 backwards from nibble 0, otherwise nothing. So steep 4bpp lines
; don't draw properly.
;
; Fixed (fx_fixed, as the official emulator does): in 4 bit mode with a nibble increment on ADDR0, ADDR1's nibble
; steps in ADDR0's direction.
; Uses r13.
vera_line_step_addr0 macro
    local done, backwards, hardware

    add rdi, [rdx].state.data0_step

    cmp dword ptr [rdx].state.fx_fixed, 0
    je hardware
    test dword ptr [rdx].state.fx_4bit_mode, 1
    jz done
    vera_nibble_step <[rdx].state.data1_nibble>, <[rdx].state.data0_nibble_incr>
    jmp done

hardware:
    mov r13d, [rdx].state.data0_nibble_step
    test r13d, r13d
    jz done
    js backwards

    add edi, [rdx].state.data0_nibble
    jmp done

backwards:
    mov r13d, [rdx].state.data0_nibble
    dec r13d								; -1 from nibble 0
    movsxd r13, r13d
    add rdi, r13
done:
    and rdi, 1ffffh
endm

; Works out the flags that keep the usual data port path quick. Called when FX_CTRL or ADDR1_H is written. Uses eax, ecx.
vera_update_fx_flags macro
    local no_hop

    ; plain writes need the slow path for one-byte cycling, transparent writes or 4-bit mode
    mov eax, [rdx].state.fx_one_byte_cycling
    or eax, [rdx].state.fx_transparancy
    or eax, [rdx].state.fx_4bit_mode
    mov [rdx].state.fx_write_special, eax

    ; 16-bit hop only changes ADDR1 increments of 4 and 320
    xor eax, eax
    cmp dword ptr [rdx].state.fx_16bit_hop, 0
    je no_hop
    mov rcx, [rdx].state.data1_step
    cmp rcx, 4
    sete al
    cmp rcx, 320
    sete cl
    or al, cl
no_hop:
    mov [rdx].state.fx_hop_active, eax
endm

; Turns an increment as written (6.9 signed in bits 14:0, x32 in bit 15) into 16.16. Uses eax, ecx.
vera_fx_increment macro raw, increment
    local no_x32

    mov eax, raw
    mov ecx, eax
    shl eax, 17
    sar eax, 10								; sign extended, and moved up 7 bits to 16.16
    test ecx, 8000h
    jz no_x32
    shl eax, 5
no_x32:
    mov increment, eax
endm

; Steps the X and Y/X2 positions by their increments. The hardware's positions are 11.9, so they wrap at 11 bits.
; Uses eax.
vera_fx_step_positions macro
    mov eax, [rdx].state.fx_x_increment
    add eax, [rdx].state.fx_x_position
    and eax, 07ffffffh
    mov [rdx].state.fx_x_position, eax

    mov eax, [rdx].state.fx_y_increment
    add eax, [rdx].state.fx_y_position
    and eax, 07ffffffh
    mov [rdx].state.fx_y_position, eax
endm

; Polygon filler: ADDR1 = ADDR0 + X1 (signed). In 4-bit mode that's X1 / 2, and the nibble is X1's bit 0. Sets rdi
; and data1_nibble. Uses eax, ecx, changes rax.
vera_fx_poly_addr1 macro
    local eight_bit

    mov eax, [rdx].state.fx_x_position
    shl eax, 5
    sar eax, 21								; X1's 11 bit integer, signed
    xor ecx, ecx
    test dword ptr [rdx].state.fx_4bit_mode, 1
    jz eight_bit
    mov ecx, eax
    and ecx, 1
    sar eax, 1
eight_bit:
    mov [rdx].state.data1_nibble, ecx
    movsxd rax, eax
    mov rdi, [rdx].state.data0_address
    add rdi, rax
    and rdi, 1ffffh
endm

; Affine helper: the address of the pixel at the X/Y position, looked up through the map (one byte per tile) into the
; 8x8 tile data. Outside the map it wraps, or uses tile 0 with clip on. Sets rdi and data1_nibble. Uses eax, ecx,
; r12, r13, changes rax.
vera_fx_affine_addr1 macro
    local inside, eight_bit, done

    mov r12d, [rdx].state.fx_x_position
    shr r12d, 16
    and r12d, 7ffh							; X
    mov r13d, [rdx].state.fx_y_position
    shr r13d, 16
    and r13d, 7ffh							; Y

    ; map address, wrapping to the map size
    mov ecx, [rdx].state.fx_map_shift
    mov eax, r13d
    shr eax, 3
    bzhi eax, eax, ecx
    shlx eax, eax, ecx						; tile y * map width
    mov edi, r12d
    shr edi, 3
    bzhi edi, edi, ecx
    add eax, edi							; + tile x
    add eax, [rdx].state.fx_map_base
    and eax, 1ffffh
    mov rdi, [rdx].state.vram_ptr
    movzx eax, byte ptr [rdi + rax]			; tile index

    ; with clip on, outside the map is tile 0
    add ecx, 3
    mov edi, r12d
    or edi, r13d
    shrx edi, edi, ecx
    test edi, edi
    jz inside
    cmp dword ptr [rdx].state.fx_affine_clip, 0
    je inside
    xor eax, eax
inside:

    and r12d, 7								; x in the tile
    and r13d, 7								; y in the tile
    test dword ptr [rdx].state.fx_4bit_mode, 1
    jz eight_bit

    shl eax, 5								; 32 bytes a tile
    lea eax, [rax + r13 * 4]
    mov ecx, r12d
    shr ecx, 1
    add eax, ecx
    and r12d, 1
    mov [rdx].state.data1_nibble, r12d
    jmp done

eight_bit:
    shl eax, 6								; 64 bytes a tile
    lea eax, [rax + r13 * 8]
    add eax, r12d
    mov dword ptr [rdx].state.data1_nibble, 0

done:
    add eax, [rdx].state.fx_tile_base
    and eax, 1ffffh
    mov edi, eax
endm

; FX_POLY_FILL_L and FX_POLY_FILL_H (DCSEL 5, read only), written to $9F2B and $9F2C for reading. Follows top.v and
; addr_data.v: the fill length is Y/X2 - X1 in 10 bits, both 9:8 set means it's negative. Uses eax, ecx, r13.
vera_fx_fill_length macro
    local four_bit, four_bit_not_2bit, four_bit_pos, low_bits, overflow, store, eight_short

    mov ecx, [rdx].state.fx_y_position
    shr ecx, 16
    mov eax, [rdx].state.fx_x_position
    shr eax, 16
    sub ecx, eax
    and ecx, 3ffh							; fill length

    mov r13d, ecx							; FX_POLY_FILL_H, fill length 9:3 in 7:1
    shr r13d, 2
    and r13d, 0feh
    mov byte ptr [rsi + 9f2ch], r13b

    xor r13d, r13d
    test dword ptr [rdx].state.fx_4bit_mode, 1
    jnz four_bit

    cmp ecx, 16								; 8-bit: bit 7 is 16 or more, bit 4 is fill length 3
    jb eight_short
    or r13d, 80h
eight_short:
    mov eax, ecx
    and eax, 8
    shl eax, 1
    or r13d, eax
    jmp low_bits

four_bit:
    cmp dword ptr [rdx].state.fx_addr_mode, 2
    jne four_bit_not_2bit
    cmp dword ptr [rdx].state.fx_2bit_polygon, 0
    je four_bit_not_2bit

    mov eax, [rdx].state.fx_y_position		; 2-bit polygon: bit 7 is Y/X2's half pixel, bit 0 is X1's
    shr eax, 8
    and eax, 80h
    or r13d, eax
    mov eax, [rdx].state.fx_x_position
    shr eax, 15
    and eax, 1
    or r13d, eax
    jmp four_bit_pos

four_bit_not_2bit:
    cmp ecx, 8								; 4-bit: bit 7 is 8 or more
    jb four_bit_pos
    or r13d, 80h

four_bit_pos:
    mov eax, [rdx].state.fx_x_position		; bit 4 is X1's bit 2
    shr eax, 14
    and eax, 10h
    or r13d, eax

low_bits:
    mov eax, ecx
    shr eax, 8
    cmp eax, 3
    je overflow

    mov eax, ecx							; fill length 2:0 in 3:1
    and eax, 7
    shl eax, 1
    or r13d, eax
    mov eax, [rdx].state.fx_x_position		; X1's 1:0 in 6:5
    shr eax, 11
    and eax, 60h
    or r13d, eax
    jmp store

overflow:
    and r13d, 81h							; negative, only bits 7 and 0 are left
store:
    mov byte ptr [rsi + 9f2bh], r13b
endm

; The cache fill shift for the cache index in eax: a byte in 8-bit mode, a nibble in 4-bit mode. Uses ecx.
vera_fx_cache_shift macro
    mov ecx, [rdx].state.fx_4bit_mode
    xor ecx, 0fffffffeh						; 8-bit mode ignores the nibble index
    and eax, ecx
    shl eax, 2
    mov [rdx].state.fx_cache_fill_shift, eax
endm

; Moves the cache on a byte, or between two bytes in two-byte mode. The nibble index stays. Uses eax, ecx.
vera_fx_cache_next_byte macro
    local two_byte, done

    mov eax, [rdx].state.fx_cache_index
    test dword ptr [rdx].state.fx_2byte_cache_incr, 1
    jnz two_byte
    add eax, 2
    and eax, 0111b
    jmp done
two_byte:
    xor eax, 2
done:
    mov [rdx].state.fx_cache_index, eax
    vera_fx_cache_shift
endm

; Accumulate, from FX_MULT bit 6 or reading FX_ACCUM. With the multiplier on it adds or subtracts the product, with it
; off the accumulator loads the cache (mult_accum.v uses OLOAD to pass the cache through). fx_fixed always uses the
; product, as the official emulator does. Uses eax, ecx.
vera_fx_accumulate macro
    local multiply, subtract, done

    mov ecx, [rdx].state.fx_cache
    cmp dword ptr [rdx].state.fx_multiplier_enable, 0
    jne multiply
    cmp dword ptr [rdx].state.fx_fixed, 0
    jne multiply
    mov [rdx].state.fx_accumulator, ecx
    jmp done

multiply:
    movsx eax, cx
    sar ecx, 16
    imul eax, ecx
    test dword ptr [rdx].state.fx_accumulate_direction, 1
    jnz subtract
    add [rdx].state.fx_accumulator, eax
    jmp done
subtract:
    sub [rdx].state.fx_accumulator, eax
done:
endm

; Polygon filler, reading DATA0 or DATA1 steps X1 and X2. Reading DATA1 also sets ADDR1 = ADDR0 + X1 (rdi), except
; in 2-bit poke mode where reads don't step. Reading DATA0 with one-byte cycling and cache fill off moves the cache on,
; respecting two-byte mode, or with fx_fixed always a byte, as the official emulator does.
; Uses eax, ecx, r13, reloads rax.
vera_fx_poly_read macro port
    local no_cycle, no_fill_regs, done, fixed_cycle

    if port eq 1
        cmp dword ptr [rdx].state.fx_2bit_poke, 0
        jne done
    endif

    vera_fx_step_positions

    if port eq 1
        vera_fx_poly_addr1
    else
        cmp dword ptr [rdx].state.fx_one_byte_cycling, 0
        je no_cycle
        cmp dword ptr [rdx].state.fx_cache_fill, 0
        jne no_cycle
        cmp dword ptr [rdx].state.fx_fixed, 0
        jne fixed_cycle
        vera_fx_cache_next_byte
        jmp no_cycle
    fixed_cycle:
        mov eax, [rdx].state.fx_cache_index
        add eax, 2								; next byte, the nibble index stays
        and eax, 0111b
        mov [rdx].state.fx_cache_index, eax
        vera_fx_cache_shift
    no_cycle:
    endif

    cmp byte ptr [rdx].state.dcsel, 5			; keep FX_POLY_FILL_L/H up to date if they can be read
    jne no_fill_regs
    vera_fx_fill_length
no_fill_regs:
done:
    mov rax, [rdx].state.vram_ptr
endm

; After a DCSEL 4 position write: in polygon mode ADDR1 = ADDR0 + X1, in affine mode ADDR1 is the pixel's address.
; Uses eax, ecx, r12, r13, rdi, and rbx, rsi when ADDRSEL is 1.
vera_fx_position_written macro
    local affine, set_addr1, done

    mov eax, [rdx].state.fx_addr_mode
    cmp eax, 2
    jb done
    ja affine
    vera_fx_poly_addr1
    jmp set_addr1
affine:
    vera_fx_affine_addr1
set_addr1:
    mov [rdx].state.data1_address, rdi
    mov rsi, [rdx].state.memory_ptr
    vera_update_data1
    cmp byte ptr [rdx].state.addrsel, 1
    jne done
    vera_setaddress 1
done:
endm

; Sets ADDRx_L/M/H for a port, eg when ADDRSEL changes.
vera_setaddress macro port
    local search_loop, match, not_negative, no_nibble_incr
    vera_port_fields port

    xor r12, r12						; use r12 to store if decr should be set
    mov rax, port_step

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
    mov rdi, port_address
    or rdi, rbx

    mov rsi, [rdx].state.memory_ptr
    mov [rsi+ADDRx_L], di

    shr rdi, 16
    or dil, r12b						; or on the DECR bit

    mov eax, port_nibble				; nibble address
    lea edi, [rdi + rax * 2]
    mov eax, port_nibble_incr			; nibble increment, a step of 0 can't carry DECR so it comes from here too
    test eax, eax
    jz no_nibble_incr
    or dil, 0100b
    jns no_nibble_incr
    or dil, 1000b
no_nibble_incr:
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

; A change to a layer's mode mid line leaves the fetch already made for the old mode, eg the position through the
; tile or a bitmap's VRAM address, which the new renderer can't use; the 4bpp renderer indexes past its jump table.
; So start the fetch again in the new mode from the current x, the same as a new line does.
restart_layer_fetch macro num
    local done
    cmp dword ptr [rdx].state.layer&num&_state, STATE_WAIT_START
    je done                                         ; line already complete

    mov dword ptr [rdx].state.layer&num&_tiledone, -1
    mov dword ptr [rdx].state.layer&num&_tilecount, 0

    mov eax, STATE_FETCH_MAP
    mov ebx, STATE_FETCH_BITMAP
    cmp byte ptr [rdx].state.layer&num&_bitmapMode, 0
    cmovne eax, ebx
    mov dword ptr [rdx].state.layer&num&_state, eax
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

    ; removed, this will be set to zero at the start of emulation, no need to clear each time we step
    ;mov [rdx].state.frame_count, 0		; always start with 0 frames, as we use this for timing.

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
    vera_setaddress 0
    mov byte ptr [rsi+CTRL], 0
    jmp addr_done

set_address1:
    vera_setaddress 1
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
    mov ebx, dword ptr [rdx].state.layer0_t256c
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
    ;mov al, byte ptr [rdx].state.interrupt_vsync
    ;mov bl, byte ptr [rdx].state.interrupt_line
    ;shl bl, 1
    ;or al, bl
    ;mov bl, byte ptr [rdx].state.interrupt_spcol
    ;shl bl, 2
    ;or al, bl
    ;mov bl, byte ptr [rdx].state.interrupt_aflow
    ;shl bl, 3
    ;or al, bl

    mov eax, dword ptr [rdx].state.interrupt_mask
    and eax, 01111b                 ; vsync, line, spcol and aflow enables

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

    or byte ptr [rsi + ISR], INTERRUPT_AFLOW	; set AFLOW but only on initial
    or dword ptr [rdx].state.interrupt_hit, INTERRUPT_AFLOW

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

vera_write_dataport macro port
local cannot_cache_write, cache_write, fx_4bit_pixels, no_transparancy, done, no_multiplication, store, no_cycling, nibble_write, nibble_store, cache_not_cycling
    vera_port_fields port

    cmp rdi, 01f9c0h
    jge cannot_cache_write

    test [rdx].state.fx_cache_write, 1
    jnz cache_write
cannot_cache_write:
    movzx r13, byte ptr [rsi+rbx]			; get value that has been written
    cmp dword ptr [rdx].state.fx_write_special, 0
    je store								; the usual case
    cmp rdi, 01f9c0h						; FX only changes writes to main VRAM, not the PSG, palette or sprites
    jae store

    ; one-byte cycling writes the cache byte at the cache index instead
    test dword ptr [rdx].state.fx_one_byte_cycling, 1
    jz no_cycling
    mov ecx, [rdx].state.fx_cache_index
    shr ecx, 1
    shl ecx, 3
    mov r13d, [rdx].state.fx_cache
    shrx r13d, r13d, ecx
    and r13d, 0ffh
no_cycling:

    test dword ptr [rdx].state.fx_4bit_mode, 1
    jnz nibble_write

    test dword ptr [rdx].state.fx_transparancy, 1
    jz store
    test r13d, r13d
    jz done									; transparent, 0 isn't written
    jmp store

nibble_write:
    ; 4 bit mode only writes the addressed nibble, taken from the same nibble of the value
    mov ecx, port_nibble
    neg ecx									; 0 or -1
    xor ecx, 0f0h							; $f0 for nibble 0, $..0f for nibble 1
    test dword ptr [rdx].state.fx_transparancy, 1
    jz nibble_store
    test r13d, ecx
    jz done									; transparent, a 0 nibble isn't written
nibble_store:
    and r13d, ecx
    not ecx
    and cl, byte ptr [rax + rdi]			; the other nibble from vram
    or r13b, cl
store:
    mov byte ptr [rax + rdi], r13b			; store in vram
    vera_dataupdate_stuctures
    jmp done

cache_write:
    ; one-byte cycling with cache write writes the cache byte at the cache index to all four bytes
    test dword ptr [rdx].state.fx_one_byte_cycling, 1
    jz cache_not_cycling
    mov ecx, [rdx].state.fx_cache_index
    shr ecx, 1
    shl ecx, 3
    mov r12d, [rdx].state.fx_cache
    shrx r12d, r12d, ecx
    and r12d, 0ffh
    imul r12d, r12d, 01010101h
    jmp no_multiplication

cache_not_cycling:
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

    movzx r13d, byte ptr [rsi+rbx]			; get value that has been written

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

vera_read_dataport_cache macro port
local done, fx_4bit_pixels, fx_2_byte, cache_done
        vera_port_fields port
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

        mov eax, port_nibble					; the addressed nibble, the high one for nibble 0
        xor eax, 1
        shl eax, 2
        shrx r13d, r13d, eax
        and r13d, 0fh
        mov eax, 00fh
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


; One data port, DATA0 (port 0) or DATA1 (port 1). Both work the same way, apart from the FX line helper's extra step,
; which only applies to ADDR1.
;
; rbx			address
; [rsi+rbx]		output location in main memory
; r12			previous value
; ecx			extra reads on a read-modify-write (doublestep)
; rsi			pushed by the caller, popped here
vera_port_access macro port, doublestep, write_value
    local no_read_breakpoint, no_write_breakpoint, breakpoint_step_loop, read_loop, read_loop_done, no_fx_step, not_fx, set_address
    local poke_check, poke_done, line_draw, affine, not_poly, store_port

    vera_port_fields port

    mov rdi, port_address

    if write_value eq 0
        ; check for breakpoint on read
        mov eax, [rdx].state.stepping
        or eax, [rdx].state.ignore_breakpoint
        jnz no_read_breakpoint

        mov r13, [rdx].state.vrambreakpoint_ptr
        movzx r13d, byte ptr [r13 + rdi]
        and r13d, VERA_BREAKPOINT_READ
        shl r13d, 1								; convert 0001b into BREAKPOINT_VRAM
        or [rdx].state.breakpoint_source, r13d
        jz no_read_breakpoint
        pop rsi
        pop rax									; pull the return address off
        jmp next_opcode

        no_read_breakpoint:
    endif

    if write_value eq 1
        ; check for breakpoint on write
        mov eax, [rdx].state.stepping
        or eax, [rdx].state.ignore_breakpoint
        jnz no_write_breakpoint

        if doublestep eq 1
            ; a read-modify-write writes after its ecx + 1 reads, each of which steps. Stepping changes the nibble, so
            ; it's put back afterwards.
            mov r13d, port_nibble
            push r13								; the step uses r13 for 16-bit hop
            lea eax, [ecx + 1]
            breakpoint_step_loop:
            vera_port_step port
            dec eax
            jnz breakpoint_step_loop
            pop r13
            mov port_nibble, r13d
        endif

        mov r13, [rdx].state.vrambreakpoint_ptr
        movzx r13d, byte ptr [r13 + rdi]
        and r13d, VERA_BREAKPOINT_WRITE			; BREAKPOINT_WRITE is the same as VERA_BREAKPOINT so no change needed
        or [rdx].state.breakpoint_source, r13d
        jz no_write_breakpoint

        ; breaking so reverse the change in RAM
        mov byte ptr [rsi + rbx], r12b

        pop rsi
        pop rax									; pull the return address off
        jmp next_opcode

        no_write_breakpoint:
        mov rdi, port_address					; restore rdi back to the address
    endif

    mov rax, [rdx].state.vram_ptr				; get value from vram

    if port eq 1 and write_value eq 1 and doublestep eq 0
        ; 2-bit polygon poke, after ADDR1_L was written
        cmp dword ptr [rdx].state.fx_2bit_poke, 0
        jne poke_check
        poke_done:
    endif

    if write_value eq 1 and doublestep eq 0
        vera_write_dataport port
    endif

    if write_value eq 0
        vera_read_dataport_cache port
    endif

    if doublestep eq 1
        ; a read-modify-write reads ecx + 1 times before it writes. Each read steps, the last step is the one below.
        read_loop:
        vera_read_dataport_cache port

        test ecx, ecx
        jz read_loop_done

        vera_port_step port
        dec ecx
        jmp read_loop

        read_loop_done:
    endif

    vera_port_step port

    if port eq 1
        ; FX addr1 modes
        mov r13d, [rdx].state.fx_addr_mode
        test r13d, r13d
        jz not_fx								; the usual case
        cmp r13d, 2
        jb line_draw
        ja affine

        ; polygon filler: reading DATA1 steps X1 and X2 and sets ADDR1 = ADDR0 + X1, writing uses ADDR1's increment
        if write_value eq 0 and doublestep eq 0
            vera_fx_poly_read 1
        endif
        jmp not_fx

    affine:
        ; affine helper: reading or writing DATA1 steps X and Y, then ADDR1 is the pixel's address
        vera_fx_step_positions
        vera_fx_affine_addr1
        mov rax, [rdx].state.vram_ptr
        jmp not_fx

    line_draw:
        ; line helper, ADDR1 steps on by ADDR0's step each time x passes a whole pixel
        mov r13d, [rdx].state.fx_x_position
        add r13d, [rdx].state.fx_x_increment

        test r13d, 00010000h
        jz no_fx_step

        and r13d, 0fffeffffh					; always mask out the increment bit
        mov [rdx].state.fx_x_position, r13d

        vera_line_step_addr0

        jmp not_fx

        no_fx_step:
        mov [rdx].state.fx_x_position, r13d

        not_fx:
    endif

    if port eq 0 and write_value eq 0 and doublestep eq 0
        ; polygon filler: reading DATA0 steps X1 and X2
        cmp dword ptr [rdx].state.fx_addr_mode, 2
        jne not_poly
        vera_fx_poly_read 0
        not_poly:
    endif

    if doublestep eq 1
        if write_value eq 1
            vera_write_dataport port
        endif

        vera_port_step port						; perform second step
    endif

    store_port:
    mov port_address, rdi

    mov r13b, byte ptr [rax+rdi]
    mov [rsi+rbx], r13b							; store in ram

    xor r13, r13								; clear r13b, as we use this to detect if we need to call vera

    cmp [rdx].state.addrsel, port
    je set_address
    pop rsi
    ret

set_address:
    mov word ptr [rsi + ADDRx_L], di			; write L and M bytes

    shr rdi, 16									; need to add bottom bit of the address, so shift and mask
    and rdi, 1
    movzx eax, byte ptr [rsi + ADDRx_H]			; keep the increment, DECR and nibble increment
    and eax, 0fch
    or edi, eax
    mov eax, port_nibble						; and the current nibble
    lea edi, [rdi + rax * 2]
    mov byte ptr [rsi + ADDRx_H], dil

    xor r13, r13								; clear r13b, as we use this to detect if we need to call vera
    pop rsi
    ret

    if port eq 1 and write_value eq 1 and doublestep eq 0
    poke_check:
        cmp dword ptr [rdx].state.fx_addr_mode, 2	; only in polygon mode
        jne poke_done

        ; write the value's bits 7:6 pick which 2 bits of the byte at ADDR1 get the cache byte's 2 bits. ADDR1 doesn't
        ; move, and nibbles, transparency and cache writes don't apply.
        mov dword ptr [rdx].state.fx_2bit_poke, 0
        movzx ecx, byte ptr [rsi+rbx]
        shr ecx, 6
        add ecx, ecx
        mov r13d, 0c0h
        shrx r13d, r13d, ecx					; the 2 bits
        mov eax, [rdx].state.fx_cache_index
        shr eax, 1
        shl eax, 3
        mov ecx, [rdx].state.fx_cache
        shrx ecx, ecx, eax						; cache byte
        and ecx, r13d
        not r13d
        mov rax, [rdx].state.vram_ptr
        and r13b, byte ptr [rax + rdi]
        or r13b, cl
        mov byte ptr [rax + rdi], r13b
        vera_dataupdate_stuctures
        jmp store_port
    endif
endm

; rbx			address
; [rsi+rbx]		output location in main memory
; r12			previous value
;
; should only be called if data0\data1 is read\written.
vera_dataaccess_body macro doublestep, write_value
    local step_data1

    push rsi
    mov rsi, [rdx].state.memory_ptr				; we need memory context;

    cmp rbx, DATA0
    jne step_data1

    vera_port_access 0, doublestep, write_value

step_data1:
    vera_port_access 1, doublestep, write_value
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

    vera_fx_accumulate

done:
    ret
vera_afterread_9f2a endp

; add on the high bit of the scanline to vram_data
vera_afterread_9f28 proc
    call vera_render_display

    lea rax, [scanline_lookup]

    movzx ecx, word ptr [rdx].state.beam_y
    movzx eax, byte ptr [rax + rcx * 2]

    mov dword ptr [rdx].state.vram_data, eax

    ret
vera_afterread_9f28 endp

; set the vram_data to the scanline
vera_afterread_9f26 proc
    call vera_render_display

    lea rax, [scanline_lookup]

    movzx ecx, word ptr [rdx].state.beam_y
    movzx ecx, byte ptr [rax + rcx * 2 + 1]

    mov eax, dword ptr [rdx].state.vram_data    ; get existing value and mask off bit 6
    and eax, 10111111b
    or  eax, ecx                                ; put the high bit of the scanline on
    mov dword ptr [rdx].state.vram_data, eax

    ret
vera_afterread_9f26 endp

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
    movzx r13d, byte ptr [rsi+rbx]
    cmp byte ptr [rdx].state.addrsel, 0

    jnz write_data1
    mov byte ptr [rdx].state.data0_address, r13b
    vera_update_data0
    ret
write_data1:
    ; 16-bit hop remembers where ADDR1 starts
    cmp dword ptr [rdx].state.fx_16bit_hop, 0
    je no_hop
    mov eax, r13d
    and eax, 3
    mov [rdx].state.fx_hop_start, eax
no_hop:

    ; 2-bit polygon mode only sets the low 2 bits, and the next DATA1 write pokes
    cmp dword ptr [rdx].state.fx_addr_mode, 2
    jne not_poke
    test dword ptr [rdx].state.fx_4bit_mode, 1
    jz not_poke
    cmp dword ptr [rdx].state.fx_2bit_polygon, 0
    je not_poke
    mov dword ptr [rdx].state.fx_2bit_poke, 1
    mov al, byte ptr [rdx].state.data1_address
    and al, 0fch
    and r13b, 3
    or r13b, al
    mov byte ptr [rsi+rbx], r13b					; reads back as the address it's set to
not_poke:

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

; r13d is the value written to ADDRx_H: increment (7-4), DECR (3), nibble increment (2), nibble address (1), bit 16 (0)
vera_update_addrh_port macro port
    local no_decr
    vera_port_fields port

    mov eax, r13d
    and eax, 1
    mov byte ptr port_address + 2, al					; address bit 16

    mov eax, r13d
    shr eax, 1
    and eax, 1
    mov port_nibble, eax

    mov r12d, r13d
    and r12d, 11110000b									; mask off the index
    shr r12d, 3											; index in the table, not 4 as its a word
    lea rax, vera_step_table
    movzx r12d, word ptr [rax + r12]					; get value from table

    xor eax, eax
    test r13d, 0100b									; nibble increment
    setnz al

    test r13d, 1000b									; DECR
    jz no_decr
    neg r12
    neg eax
no_decr:
    mov qword ptr port_step, r12
    mov port_nibble_incr, eax
    vera_update_nibble_step port

    if port eq 1
        vera_update_fx_flags					; 16-bit hop depends on ADDR1's increment
    endif

    if port eq 0
        vera_update_data0
    else
        vera_update_data1
    endif
    ret
endm

vera_update_addrh proc
    movzx r13d, byte ptr [rsi+rbx]						; value that has been written, all bits are kept
    cmp byte ptr [rdx].state.addrsel, 0					; data 0 or 1?
    jnz write_data1

    vera_update_addrh_port 0

write_data1:
    vera_update_addrh_port 1
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
    vera_setaddress 0
    jmp addr_done

set_addr:
    mov byte ptr [rdx].state.addrsel, 1
    vera_setaddress 1

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

    mov ebx, [rdx].state.fx_16bit_hop
    shl ebx, 3
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

dc_sel5:
    ; FX_POLY_FILL_L/H read at $9F2B/$9F2C, the rest is the version
    mov byte ptr [rsi+DC_VER0], 056h
    mov byte ptr [rsi+DC_VER1], VERA_VERSION_L
    vera_fx_fill_length
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
    qword dc_sel5 - dc_sel_table
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

    ; set the mask based on what we want
    mov eax, dword ptr [rdx].state.interrupt_mask
    and eax, 0fffffff0h
    mov ebx, r13d
    and ebx, 1111b
    or eax, ebx
    mov dword ptr [rdx].state.interrupt_mask, eax

    ;xor rax, rax
    ;bt r13, 0
    ;setc al
    ;mov byte ptr [rdx].state.interrupt_vsync, al
    
    ;xor rax, rax
    ;bt r13, 1
    ;setc al
    ;mov byte ptr [rdx].state.interrupt_line, al

    ;xor rax, rax
    ;bt r13, 2
    ;setc al
    ;mov byte ptr [rdx].state.interrupt_spcol, al

    ;xor rax, rax
    ;bt r13, 3
    ;setc al
    ;mov byte ptr [rdx].state.interrupt_aflow, al

    xor rax, rax
    bt r13, 7
    setc al
    mov byte ptr [rdx].state.interrupt_linenum + 1, al

    ;mov r13b, byte ptr [rdx].state.interrupt_spcol_hit
    ;shl r13, 1
    ;or r13b, byte ptr [rdx].state.interrupt_line_hit
    ;shl r13, 1
    ;or r13b, byte ptr [rdx].state.interrupt_vsync_hit

    ;xor rax, rax
    ;mov bl, byte ptr [rsi+IEN]
    ;and r13b, bl
    ;setnz al

    ;mov byte ptr [rdx].state.interrupt, al

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

    mov eax, r13d
    and eax, 01000b
    shr eax, 3
    mov [rdx].state.fx_16bit_hop, eax

    ; the nibble increments only apply in 4 bit mode
    vera_update_nibble_step 0
    vera_update_nibble_step 1
    vera_update_fx_flags
    mov eax, [rdx].state.fx_4bit_mode

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
    ; FX_X_INCR_L: the increment's bits 7:0
    movzx r13, byte ptr [rsi+rbx]
    mov byte ptr [rsi+rbx], r12b			; write only
    mov byte ptr [rdx].state.fx_x_incr_raw, r13b
    vera_fx_increment [rdx].state.fx_x_incr_raw, [rdx].state.fx_x_increment
    ret

dc_sel4:
    ; FX_X_POS_L: X's bits 7:0
    movzx r13, byte ptr [rsi+rbx]
    mov byte ptr [rsi+rbx], r12b			; write only
    mov byte ptr [rdx].state.fx_x_position + 2, r13b
    vera_fx_position_written
    ret

dc_sel5:
    ; X's sub pixel bits -1 to -8
    movzx r13, byte ptr [rsi+rbx]
    mov byte ptr [rsi+rbx], r12b			; write only
    mov byte ptr [rdx].state.fx_x_position + 1, r13b
    vera_fx_fill_length
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
    qword dc_sel4 - dc_sel_table
    qword dc_sel5 - dc_sel_table
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
    ; FX_TILEBASE: tile base (16:11), affine clip, 2-bit polygon
    movzx r13, byte ptr [rsi+rbx]
    mov byte ptr [rsi+rbx], r12b			; write only
    mov eax, r13d
    and eax, 0fch
    shl eax, 9
    mov [rdx].state.fx_tile_base, eax
    mov eax, r13d
    shr eax, 1
    and eax, 1
    mov [rdx].state.fx_affine_clip, eax
    and r13d, 1
    mov [rdx].state.fx_2bit_polygon, r13d
    ret

dc_sel3:
    ; FX_X_INCR_H: x32, then the increment's bits 14:8
    movzx r13, byte ptr [rsi+rbx]
    mov byte ptr [rsi+rbx], r12b			; write only
    mov byte ptr [rdx].state.fx_x_incr_raw + 1, r13b
    mov eax, r13d
    shr eax, 7
    mov [rdx].state.fx_x_mult_32, eax
    vera_fx_increment [rdx].state.fx_x_incr_raw, [rdx].state.fx_x_increment

    mov eax, [rdx].state.fx_addr_mode
    cmp eax, 1
    je dc_sel_line_mode
    cmp eax, 2
    je dc_sel_poly_fill
    ret

dc_sel_line_mode:
    mov eax, [rdx].state.fx_x_position
    and eax, 0fffe0000h						; clear bit '0', the overflow
    or eax, 00008000h
    mov [rdx].state.fx_x_position, eax
    ret

dc_sel_poly_fill:
    mov eax, [rdx].state.fx_x_position		; X1's sub pixel is half a pixel
    and eax, 0ffff0000h
    or eax, 00008000h
    mov [rdx].state.fx_x_position, eax
    ret

dc_sel4:
    ; FX_X_POS_H: the -9 bit in bit 7, X's bits 10:8 in 2:0
    movzx r13, byte ptr [rsi+rbx]
    mov byte ptr [rsi+rbx], r12b			; write only
    mov eax, [rdx].state.fx_x_position
    and eax, 00ffff00h
    mov ecx, r13d
    and ecx, 7
    shl ecx, 24
    or eax, ecx
    and r13d, 80h
    or eax, r13d
    mov [rdx].state.fx_x_position, eax
    vera_fx_position_written
    ret

dc_sel5:
    ; Y/X2's sub pixel bits -1 to -8
    movzx r13, byte ptr [rsi+rbx]
    mov byte ptr [rsi+rbx], r12b			; write only
    mov byte ptr [rdx].state.fx_y_position + 1, r13b
    vera_fx_fill_length
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
    qword dc_sel2 - dc_sel_table
    qword dc_sel3 - dc_sel_table
    qword dc_sel4 - dc_sel_table
    qword dc_sel5 - dc_sel_table
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

dc_sel2:
    ; FX_MAPBASE: map base (16:11), map size 2, 8, 32 or 128 tiles
    movzx r13, byte ptr [rsi+rbx]
    mov byte ptr [rsi+rbx], r12b			; write only
    mov eax, r13d
    and eax, 0fch
    shl eax, 9
    mov [rdx].state.fx_map_base, eax
    and r13d, 3
    lea eax, [r13 * 2 + 1]
    mov [rdx].state.fx_map_shift, eax
    ret

dc_sel3:
    ; FX_Y_INCR_L: the increment's bits 7:0
    movzx r13, byte ptr [rsi+rbx]
    mov byte ptr [rsi+rbx], r12b			; write only
    mov byte ptr [rdx].state.fx_y_incr_raw, r13b
    vera_fx_increment [rdx].state.fx_y_incr_raw, [rdx].state.fx_y_increment
    ret

dc_sel4:
    ; FX_Y_POS_L: Y's bits 7:0
    movzx r13, byte ptr [rsi+rbx]
    mov byte ptr [rsi+rbx], r12b			; write only
    mov byte ptr [rdx].state.fx_y_position + 2, r13b
    vera_fx_position_written
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
    qword dc_sel2 - dc_sel_table
    qword dc_sel3 - dc_sel_table
    qword dc_sel4 - dc_sel_table
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

    ; cache byte index (3:2) and nibble index (1), kept together as one 3 bit index. 8-bit mode ignores the nibble.
    mov eax, r13d
    shr eax, 1
    and eax, 0111b
    mov [rdx].state.fx_cache_index, eax
    vera_fx_cache_shift

    mov eax, r13d
    and eax, 00100000b
    shr eax, 5
    mov [rdx].state.fx_accumulate_direction, eax

    test r13d, 01000000b
    jz no_latch
    vera_fx_accumulate

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

dc_sel3:
    ; FX_Y_INCR_H: x32, then the increment's bits 14:8
    movzx r13, byte ptr [rsi+rbx]
    mov byte ptr [rsi+rbx], r12b			; write only
    mov byte ptr [rdx].state.fx_y_incr_raw + 1, r13b
    mov eax, r13d
    shr eax, 7
    mov [rdx].state.fx_y_mult_32, eax
    vera_fx_increment [rdx].state.fx_y_incr_raw, [rdx].state.fx_y_increment

    ; in line draw and polygon mode Y/X2's sub pixel is half a pixel
    mov eax, [rdx].state.fx_addr_mode
    dec eax
    cmp eax, 1
    ja y_incr_done
    mov eax, [rdx].state.fx_y_position
    and eax, 0ffff0000h
    or eax, 00008000h
    mov [rdx].state.fx_y_position, eax
y_incr_done:
    ret

dc_sel4:
    ; FX_Y_POS_H: the -9 bit in bit 7, Y's bits 10:8 in 2:0
    movzx r13, byte ptr [rsi+rbx]
    mov byte ptr [rsi+rbx], r12b			; write only
    mov eax, [rdx].state.fx_y_position
    and eax, 00ffff00h
    mov ecx, r13d
    and ecx, 7
    shl ecx, 24
    or eax, ecx
    and r13d, 80h
    or eax, r13d
    mov [rdx].state.fx_y_position, eax
    vera_fx_position_written
    ret

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
    qword dc_sel3 - dc_sel_table
    qword dc_sel4 - dc_sel_table
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
    restart_layer_fetch 0

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
    restart_layer_fetch 0

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
    restart_layer_fetch 1
    
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
    restart_layer_fetch 1

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
    ; ISR can only reset the lower 3 bits of the interrupt
    movzx r13d, byte ptr [rsi+rbx]
    mov ecx, r13d

    and r13b, 0111b             ; mask off what can be disabled
    xor r13d, 0ffffffffh

    mov eax, dword ptr [rdx].state.interrupt_hit
    and eax, r13d
    and dword ptr [rdx].state.interrupt_hit, eax

    and r12d, 0f0h				    ; mask out lower four bits from previous value
    and eax, 01111b                 ; what's been hit, including aflow, which a write can't clear (r13 only clears bits 0-2)
    or r12d, eax                    ; set back whast been hit
    and r12d, r13d                  ; mask off what we've just cleared
    mov byte ptr [rsi+rbx], r12b    ; store



    ;bt r13, 0
    ;jnc check_line

    ;mov [rdx].state.interrupt_vsync_hit, 0

;check_line:
    ;bt r13, 1
    ;jnc check_spcol

    ;mov [rdx].state.interrupt_line_hit, 0

;check_spcol:
    ;bt r13, 2
    ;jnc construct_isr

    ;mov [rdx].state.interrupt_spcol_hit, 0

;construct_isr:
    ;mov r13b, byte ptr [rdx].state.interrupt_spcol_hit
    ;shl r13, 1
    ;or r13b, byte ptr [rdx].state.interrupt_line_hit
    ;shl r13, 1
    ;or r13b, byte ptr [rdx].state.interrupt_vsync_hit
    ;or r13, r12						; or back on the top 5 bits.
    ;mov byte ptr [rsi+rbx], r13b

    ;xor rax, rax
    ;mov bl, byte ptr [rsi+IEN]
    ;and ebx, 0fh					; only consider bottom 4 bits.
    ;and r13b, bl

    ;setnz al

    ;mov byte ptr [rdx].state.interrupt, al

    ret
vera_update_isr endp

vera_update_audiorate proc
    movzx r13, byte ptr [rsi+rbx]
    mov [rdx].state.pcm_samplerate, r13d

    ret
vera_update_audiorate endp

vera_update_audiodata proc
    mov ecx, [rdx].state.pcm_buffercount
    cmp ecx, VERA_BUFFER_MASK
    jge full

    movzx r13, byte ptr [rsi + rbx]	        ; get value from ram

    mov eax, [rdx].state.pcm_bufferwrite
    mov ebx, [rdx].state.pcm_bufferread
    mov rbx, [rdx].state.pcm_ptr
    mov byte ptr[rbx + rax], r13b			; write to the original write index
    
    inc eax                                 ; move buffer write on one
    and eax, VERA_BUFFER_MASK
    mov [rdx].state.pcm_bufferwrite, eax

    xor eax, eax
    mov ebx, 080h

    lea r13, [ecx + 1]
    cmp r13d, VERA_BUFFER_MASK
    cmovge eax, ebx
    cmovl ecx, r13d                        ; revert inc if overflow
    mov dword ptr [rdx].state.pcm_buffercount, ecx

    and byte ptr [rsi + AUDIO_CTRL], 03fh	; clear empty bit and full bit
    or byte ptr [rsi + AUDIO_CTRL], al	    ; set buffer full if necessary

    mov eax, dword ptr [rdx].state.interrupt_hit
    mov ebx, eax
    or eax, INTERRUPT_AFLOW                 ; eax is current state /w aflow set
    and ebx, 0ffffffffh - INTERRUPT_AFLOW   ; ebx is current state /wo aflow
    cmp ecx, 0400h
    cmovge eax, ebx                         ; if greater than 0x400 then clear aflow
    mov dword ptr [rdx].state.interrupt_hit, eax

    and eax, INTERRUPT_AFLOW                ; isolate the aflow bit
    movzx rbx, byte ptr [rsi+ISR]
    and ebx, 011110111b						; clear
    or ebx, eax								; set if neccesary
    mov byte ptr [rsi+ISR], bl				; write


    ret


    mov eax, [rdx].state.pcm_bufferwrite
    mov ecx, eax
    inc eax
    and eax, VERA_BUFFER_MASK	; constrain to 4k
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
    ;mov eax, [rdx].state.pcm_bufferwrite
    ;sub eax, [rdx].state.pcm_bufferread

    ;xor ebx, ebx
    ;mov rcx, 01000b							; AFLOW flag
    ;cmp eax, 0400h
    ;cmova ecx, ebx							; clear if greater


    ; AFLOW
    mov eax, [rdx].state.pcm_bufferwrite
    mov ebx, [rdx].state.pcm_bufferread

    ; need to sign extend these from 12bit to 16bit.
    shl eax, 4
    shl ebx, 4
    sar ax, 4
    sar bx, 4

    sub ax, bx

    xor ebx, ebx
    mov rcx, INTERRUPT_AFLOW		; AFLOW flag
    cmp eax, 0400h
;    and ax, 0c00h
    cmova ecx, ebx					; clear if greater


    mov eax, dword ptr [rdx].state.interrupt_hit
    and eax, 0fffffff7h
    or eax, ecx
    mov dword ptr [rdx].state.interrupt_hit, eax

    movzx rbx, byte ptr [rsi+ISR]
    and ebx, 011110111b						; clear
    or ebx, ecx								; set if neccesary
    mov byte ptr [rsi+ISR], bl				; write

    ret
full:
    or byte ptr [rsi + AUDIO_CTRL], 080h	; set full bit

    ret

vera_update_audiodata endp

vera_update_audioctrl proc
    movzx r13d, byte ptr [rsi+rbx]

    ; handle reset
    bt r13d, 7
    jnc no_reset
    xor ebx, ebx
    mov [rdx].state.pcm_bufferwrite, ebx
    mov [rdx].state.pcm_bufferread, ebx
    mov [rdx].state.pcm_buffercount, ebx

    ; as the fifo has been cleared, set aflow
    or byte ptr [rsi + AUDIO_CTRL], 040h	; set empty flag
    or dword ptr [rdx].state.interrupt_hit, INTERRUPT_AFLOW
    or byte ptr [rsi + ISR], INTERRUPT_AFLOW

    ;xor ecx, ecx
    ;movzx rbx, byte ptr [rsi + ISR]			
    ;or ebx, 1000b							; set aflow
    ;mov byte ptr [rsi + ISR], bl
    ;and bl, byte ptr [rsi + IEN]
    ;and ebx, 1000b
    ;shr ebx, 4

    ;or byte ptr [rdx].state.interrupt, bl	; or on the interrupt

no_reset:
    ; set 16bit / stereo
    mov ebx, r13d
    and ebx, 030h
    shr ebx, 4
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

scanline_lookup:
    word 01h
    word 02h
    word 03h
    word 04h
    word 05h
    word 06h
    word 07h
    word 08h
    word 09h
    word 0ah
    word 0bh
    word 0ch
    word 0dh
    word 0eh
    word 0fh
    word 010h
    word 011h
    word 012h
    word 013h
    word 014h
    word 015h
    word 016h
    word 017h
    word 018h
    word 019h
    word 01ah
    word 01bh
    word 01ch
    word 01dh
    word 01eh
    word 01fh
    word 020h
    word 021h
    word 022h
    word 023h
    word 024h
    word 025h
    word 026h
    word 027h
    word 028h
    word 029h
    word 02ah
    word 02bh
    word 02ch
    word 02dh
    word 02eh
    word 02fh
    word 030h
    word 031h
    word 032h
    word 033h
    word 034h
    word 035h
    word 036h
    word 037h
    word 038h
    word 039h
    word 03ah
    word 03bh
    word 03ch
    word 03dh
    word 03eh
    word 03fh
    word 040h
    word 041h
    word 042h
    word 043h
    word 044h
    word 045h
    word 046h
    word 047h
    word 048h
    word 049h
    word 04ah
    word 04bh
    word 04ch
    word 04dh
    word 04eh
    word 04fh
    word 050h
    word 051h
    word 052h
    word 053h
    word 054h
    word 055h
    word 056h
    word 057h
    word 058h
    word 059h
    word 05ah
    word 05bh
    word 05ch
    word 05dh
    word 05eh
    word 05fh
    word 060h
    word 061h
    word 062h
    word 063h
    word 064h
    word 065h
    word 066h
    word 067h
    word 068h
    word 069h
    word 06ah
    word 06bh
    word 06ch
    word 06dh
    word 06eh
    word 06fh
    word 070h
    word 071h
    word 072h
    word 073h
    word 074h
    word 075h
    word 076h
    word 077h
    word 078h
    word 079h
    word 07ah
    word 07bh
    word 07ch
    word 07dh
    word 07eh
    word 07fh
    word 080h
    word 081h
    word 082h
    word 083h
    word 084h
    word 085h
    word 086h
    word 087h
    word 088h
    word 089h
    word 08ah
    word 08bh
    word 08ch
    word 08dh
    word 08eh
    word 08fh
    word 090h
    word 091h
    word 092h
    word 093h
    word 094h
    word 095h
    word 096h
    word 097h
    word 098h
    word 099h
    word 09ah
    word 09bh
    word 09ch
    word 09dh
    word 09eh
    word 09fh
    word 0a0h
    word 0a1h
    word 0a2h
    word 0a3h
    word 0a4h
    word 0a5h
    word 0a6h
    word 0a7h
    word 0a8h
    word 0a9h
    word 0aah
    word 0abh
    word 0ach
    word 0adh
    word 0aeh
    word 0afh
    word 0b0h
    word 0b1h
    word 0b2h
    word 0b3h
    word 0b4h
    word 0b5h
    word 0b6h
    word 0b7h
    word 0b8h
    word 0b9h
    word 0bah
    word 0bbh
    word 0bch
    word 0bdh
    word 0beh
    word 0bfh
    word 0c0h
    word 0c1h
    word 0c2h
    word 0c3h
    word 0c4h
    word 0c5h
    word 0c6h
    word 0c7h
    word 0c8h
    word 0c9h
    word 0cah
    word 0cbh
    word 0cch
    word 0cdh
    word 0ceh
    word 0cfh
    word 0d0h
    word 0d1h
    word 0d2h
    word 0d3h
    word 0d4h
    word 0d5h
    word 0d6h
    word 0d7h
    word 0d8h
    word 0d9h
    word 0dah
    word 0dbh
    word 0dch
    word 0ddh
    word 0deh
    word 0dfh
    word 0e0h
    word 0e1h
    word 0e2h
    word 0e3h
    word 0e4h
    word 0e5h
    word 0e6h
    word 0e7h
    word 0e8h
    word 0e9h
    word 0eah
    word 0ebh
    word 0ech
    word 0edh
    word 0eeh
    word 0efh
    word 0f0h
    word 0f1h
    word 0f2h
    word 0f3h
    word 0f4h
    word 0f5h
    word 0f6h
    word 0f7h
    word 0f8h
    word 0f9h
    word 0fah
    word 0fbh
    word 0fch
    word 0fdh
    word 0feh
    word 0ffh
    word 04000h
    word 04001h
    word 04002h
    word 04003h
    word 04004h
    word 04005h
    word 04006h
    word 04007h
    word 04008h
    word 04009h
    word 0400ah
    word 0400bh
    word 0400ch
    word 0400dh
    word 0400eh
    word 0400fh
    word 04010h
    word 04011h
    word 04012h
    word 04013h
    word 04014h
    word 04015h
    word 04016h
    word 04017h
    word 04018h
    word 04019h
    word 0401ah
    word 0401bh
    word 0401ch
    word 0401dh
    word 0401eh
    word 0401fh
    word 04020h
    word 04021h
    word 04022h
    word 04023h
    word 04024h
    word 04025h
    word 04026h
    word 04027h
    word 04028h
    word 04029h
    word 0402ah
    word 0402bh
    word 0402ch
    word 0402dh
    word 0402eh
    word 0402fh
    word 04030h
    word 04031h
    word 04032h
    word 04033h
    word 04034h
    word 04035h
    word 04036h
    word 04037h
    word 04038h
    word 04039h
    word 0403ah
    word 0403bh
    word 0403ch
    word 0403dh
    word 0403eh
    word 0403fh
    word 04040h
    word 04041h
    word 04042h
    word 04043h
    word 04044h
    word 04045h
    word 04046h
    word 04047h
    word 04048h
    word 04049h
    word 0404ah
    word 0404bh
    word 0404ch
    word 0404dh
    word 0404eh
    word 0404fh
    word 04050h
    word 04051h
    word 04052h
    word 04053h
    word 04054h
    word 04055h
    word 04056h
    word 04057h
    word 04058h
    word 04059h
    word 0405ah
    word 0405bh
    word 0405ch
    word 0405dh
    word 0405eh
    word 0405fh
    word 04060h
    word 04061h
    word 04062h
    word 04063h
    word 04064h
    word 04065h
    word 04066h
    word 04067h
    word 04068h
    word 04069h
    word 0406ah
    word 0406bh
    word 0406ch
    word 0406dh
    word 0406eh
    word 0406fh
    word 04070h
    word 04071h
    word 04072h
    word 04073h
    word 04074h
    word 04075h
    word 04076h
    word 04077h
    word 04078h
    word 04079h
    word 0407ah
    word 0407bh
    word 0407ch
    word 0407dh
    word 0407eh
    word 0407fh
    word 04080h
    word 04081h
    word 04082h
    word 04083h
    word 04084h
    word 04085h
    word 04086h
    word 04087h
    word 04088h
    word 04089h
    word 0408ah
    word 0408bh
    word 0408ch
    word 0408dh
    word 0408eh
    word 0408fh
    word 04090h
    word 04091h
    word 04092h
    word 04093h
    word 04094h
    word 04095h
    word 04096h
    word 04097h
    word 04098h
    word 04099h
    word 0409ah
    word 0409bh
    word 0409ch
    word 0409dh
    word 0409eh
    word 0409fh
    word 040a0h
    word 040a1h
    word 040a2h
    word 040a3h
    word 040a4h
    word 040a5h
    word 040a6h
    word 040a7h
    word 040a8h
    word 040a9h
    word 040aah
    word 040abh
    word 040ach
    word 040adh
    word 040aeh
    word 040afh
    word 040b0h
    word 040b1h
    word 040b2h
    word 040b3h
    word 040b4h
    word 040b5h
    word 040b6h
    word 040b7h
    word 040b8h
    word 040b9h
    word 040bah
    word 040bbh
    word 040bch
    word 040bdh
    word 040beh
    word 040bfh
    word 040c0h
    word 040c1h
    word 040c2h
    word 040c3h
    word 040c4h
    word 040c5h
    word 040c6h
    word 040c7h
    word 040c8h
    word 040c9h
    word 040cah
    word 040cbh
    word 040cch
    word 040cdh
    word 040ceh
    word 040cfh
    word 040d0h
    word 040d1h
    word 040d2h
    word 040d3h
    word 040d4h
    word 040d5h
    word 040d6h
    word 040d7h
    word 040d8h
    word 040d9h
    word 040dah
    word 040dbh
    word 040dch
    word 040ddh
    word 040deh
    word 040dfh
    word 040e0h
    word 040e1h
    word 040e2h
    word 040e3h
    word 040e4h
    word 040e5h
    word 040e6h
    word 040e7h
    word 040e8h
    word 040e9h
    word 040eah
    word 040ebh
    word 040ech
    word 040edh
    word 040eeh
    word 040efh
    word 040f0h
    word 040f1h
    word 040f2h
    word 040f3h
    word 040f4h
    word 040f5h
    word 040f6h
    word 040f7h
    word 040f8h
    word 040f9h
    word 040fah
    word 040fbh
    word 040fch
    word 040fdh
    word 040feh
    word 040ffh
    word 040ffh
    word 040ffh
    word 040ffh
    word 040ffh
    word 040ffh
    word 040ffh
    word 040ffh
    word 040ffh
    word 040ffh
    word 040ffh
    word 040ffh
    word 040ffh
    word 040ffh
    word 00h



.code
