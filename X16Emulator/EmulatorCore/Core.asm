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

;includelib      msvcrtd

.CODE

include State.asm
Include Io.asm
include Vera.asm
include Via.asm
include Banking.asm
include I2c.asm
include Smc.asm
include Spi.asm
include Rtc.asm
include Joypad.asm
include Ym.asm

EXIT_NOTSUPPORTED equ -1
EXIT_NORMAL equ 0
EXIT_UNKNOWNOPCODE equ 1
EXIT_DEBUGOPCODE equ 2
EXIT_BRKHIT equ 3
EXIT_SMC_POWEROFF equ 4
EXIT_STEPPING equ 5
EXIT_BREAKPOINT equ 6
EXIT_SMC_RESET equ 7

readonly_memory equ 0c000h - 1		; stop all writes above this location

BREAKPOINT_MASK     equ 000001011b      ; breakpoint, debugger breakpoint and exception
NOSTOP_MASK         equ      0100b               ; locations where we dont want the debugger to stop
EXCEPTION           equ     01000b
MEMORY_WRITTEN      equ    010000b          ; used by the debugger to see where write activity has occured, can be cleared on demand.
MEMORY_PROTECTION   equ   0100000b          ; used to indicate where memory has been written to in the life time of the session.
MEMORY_WRITE_VALUE  equ   0110000b          ; write this everytime
MEMORY_EXECUTION    equ  01000000b          ; execution points
MEMORY_READ         equ 010000000b          ; read this session


; rax  : scratch
; rbx  : scratch
; rcx  : scratch
; rdx  : state object 
; rsi  : current memory context
; rdi  : scratch
; r8b  : a
; r9b  : x
; r10b : y
; r11w : PC
; r12  : scratch
; r13  : scratch / use to indicate vera data0\1 read
; r14  : Clock Ticks
; r15  : Flags

; xmm1 : Vera Clock

write_state_obj macro	
    mov	byte ptr [rdx].state.register_a, r8b			; a
    mov	byte ptr [rdx].state.register_x, r9b			; x
    mov	byte ptr [rdx].state.register_y, r10b			; y
    mov	word ptr [rdx].state.register_pc, r11w			; PC
    mov [rdx].state.clock, r14							; Clock

    ; Flags
    ; read from r15 directly

    ; Carry
    mov rax, r15
    ;        NZ A P C
    and rax, 0000000100000000b
    ror rax, 8
    mov byte ptr [rdx].state.flags_carry, al

    ; Zero
    mov rax, r15
    ;        NZ A P C
    and rax, 0100000000000000b
    ror rax, 6+8
    mov byte ptr [rdx].state.flags_zero, al

    ; Negative
    mov rax, r15
    ;        NZ A P C
    and rax, 1000000000000000b
    ror rax, 7+8
    mov byte ptr [rdx].state.flags_negative, al	

    call via_write_state

    ; needs to copy current memory back to banked data
    call preserve_current_rambank
endm

read_state_obj macro
    local no_carry, no_zero, no_overflow, no_negative

    movzx r8, byte ptr [rdx].state.register_a		; a
    movzx r9, byte ptr [rdx].state.register_x		; x
    movzx r10, byte ptr [rdx].state.register_y		; y
    movzx r11, word ptr [rdx].state.register_pc		; PC
    mov r14, [rdx].state.clock						; Clock
    
    ; Flags
    xor r15, r15 ; clear flags register

    mov al, byte ptr [rdx].state.flags_carry
    test al, al
    jz no_carry
    ;       NZ A P C
    or r15, 0000000100000000b
no_carry:

    mov al, byte ptr [rdx].state.flags_zero
    test al, al
    jz no_zero
    ;       NZ A P C
    or r15, 0100000000000000b
no_zero:

    mov al, byte ptr [rdx].state.flags_negative
    test al, al
    jz no_negative
    ;       NZ A P C
    or r15, 1000000000000000b

no_negative:

endm


store_registers macro
    push rbx
    push rbp
    push rsi
    push rdi
    push r12
    push r13
    push r14
    push r15
endm

restore_registers macro
    pop r15
    pop r14
    pop r13
    pop r12
    pop rdi
    pop rsi
    pop rbp
    pop rbx
endm

public asm_func


asm_func proc state_ptr:QWORD
    mov rdx, rsi						; move state to rdx

    store_registers

    push rdx

    ; see if lahf is supported. if not return -1.
    ; LAHF
    mov eax, 80000001h
    cpuid
    test ecx,1           ; Is bit 0 (the "LAHF-SAHF" bit) set?
    je not_supported     ; no, LAHF is not supported

    ; AVX
    mov eax, 1
    cpuid
    and ecx, 018000000h
    cmp ecx, 018000000h
    jne not_supported

    mov ecx, 0
    xgetbv
    and eax, 06h
    cmp eax, 06h
    jne not_supported

    pop rdx
    
    push rdx
    push rcx
    call qword ptr [rdx].state.get_ticks
    pop rcx
    pop rdx

    ; get base ticks to compare against, only do this on the iniital start.
    mov ebx, [rdx].state.initial_startup
    test ebx, ebx
    jz set_adjustment

    mov [rdx].state.base_ticks, rax

    jmp clock_done

set_adjustment:
    ; set the time adjustment that is applied to the getticks to account for debugging time
    sub rax, [rdx].state.clock_pause
    add [rdx].state.base_ticks, rax     ; increase base ticks by the time we were paused.

clock_done:

    call vera_init
    call via_init
    
    read_state_obj

    mov rsi, [rdx].state.memory_ptr		; reset rsi so it points to memory

    ; setup banks
    call copy_rambank_to_memory
    call copy_rombank_to_memory

    mov dword ptr [rdx].state.stack_breakpoint_hit, 0
    mov [rdx].state.ignore_breakpoint, 1
    mov dword ptr [rdx].state.exit_code, 0

    mov dword ptr [rdx].state.initial_startup, 0

    jmp skip_stepping
main_loop::
    mov rbx, qword ptr [rdx].state.breakpoint_ptr
    test byte ptr[rbx + r11 * 4], NOSTOP_MASK
    jnz skip_stepping

    test [rdx].state.stepping, 1
    jnz step_exit
skip_stepping:    
    ; check for control
    cmp dword ptr [rdx].state.control, 1
    ; 0: run
    ; 1: wait
    ; 2: finished
    jl cpu_running
    jg exit_loop

    ; if we're in a wait loop and holding for a control pulse, then we do this seperatly as we need to
    ; adjust the time base
    pushf
    push rsi
    push r15
    push r14
    push r13
    push r12
    push r11
    push r10
    push r9
    push r8

    push rdx
    push rcx
    call qword ptr [rdx].state.get_ticks
    pop rcx
    pop rdx

    mov [rdx].state.clock_pause, rax

    pop r8
    pop r9
    pop r10
    pop r11
    pop r12
    pop r13
    pop r14
    pop r15
    pop rsi
    popf

wait_loop:
    ; check for control
    cmp dword ptr [rdx].state.control, 1
    ; 0: run
    ; 1: wait
    ; 2: finished
    jl wait_complete
    jg exit_loop

    pause

    clflushopt [rdx].state.control

    jmp wait_loop	; spin while waiting for control

wait_complete:
    pushf
    push rsi
    push r15
    push r14
    push r13
    push r12
    push r11
    push r10
    push r9
    push r8

    push rdx
    push rcx
    call qword ptr [rdx].state.get_ticks
    pop rcx
    pop rdx

    sub rax, [rdx].state.clock_pause
    add [rdx].state.base_ticks, rax     ; increase base ticks by the time we were paused.

    pop r8
    pop r9
    pop r10
    pop r11
    pop r12
    pop r13
    pop r14
    pop r15
    pop rsi
    popf

cpu_running:
    mov qword ptr [rdx].state.clock_previous, r14	; need prev clock so we know the delta

    ; check for interrupt
    movzx rcx, byte ptr [rdx].state.cpu_waiting		; set rcx here, so handle_interrupt knows if we're waiting

    cmp byte ptr [rdx].state.nmi_previous, 0
    jne nmi_already_set
    cmp byte ptr [rdx].state.nmi, 0
    jne handle_nmi
nmi_already_set:

    cmp byte ptr [rdx].state.interrupt, 0
    jne handle_interrupt


    test rcx, rcx
    jnz cpu_is_waiting				; if we're waiting, dont process next opcode

next_opcode::
    ; if we're stepping, we dont check for breakpoints
    mov eax, [rdx].state.stepping
    ;test eax, eax
    or eax, [rdx].state.ignore_breakpoint
    jnz dont_test_breakpoint

    ; check for breakpoint
    mov rbx, qword ptr [rdx].state.breakpoint_ptr
    test byte ptr[rbx + r11 * 4], BREAKPOINT_MASK

    jnz breakpoint_exit

    ; check for stack breakpoint
    mov ebx, dword ptr [rdx].state.stack_breakpoint_hit
    test ebx, ebx
    jnz breakpoint_exit

dont_test_breakpoint:
    mov [rdx].state.ignore_breakpoint, 0
    movzx rbx, byte ptr [rsi+r11]	; Get opcode

    mov rax, qword ptr [rdx].state.breakpoint_ptr
    or dword ptr [rax + r11 * 4], MEMORY_EXECUTION
   
    ;cmp r11, 0E38Dh
    ;jne debug_skip
    ;mov rbx, 0dbh
    ;debug_skip:

    ;--------------------------- START DEBUG CAPTURE ------------------------
    ;
    ; STORE DEBUG INFO
    ;
    ; PC Opcode+2bytes, A, X, Y
    ;
    push rbx
    mov rdi, [rdx].state.history_ptr
    mov rcx, [rdx].state.history_pos
    add rdi, rcx
    mov [rdx].state.debug_pos, rdi
    add rcx, 16
    and rcx, (1024*16)-1
    mov [rdx].state.history_pos, rcx
    mov word ptr [rdi], r11w		; PC
    mov byte ptr [rdi+2], bl		; Opcode
    mov al, byte ptr [rsi+1]
    mov byte ptr [rdi+3], al		; store rom
    mov al, byte ptr [rsi+0]
    mov byte ptr [rdi+4], al		; store ram
    mov ax, word ptr [rsi + r11 + 1]
    mov word ptr [rdi+8], ax        ; parameters

    mov byte ptr [rdi+5], r8b		; A
    mov byte ptr [rdi+6], r9b		; X
    mov byte ptr [rdi+7], r10b		; Y

    mov	al, 00100000b ; bits that are always set

    ; carry
    bt r15w, 0 +8
    jnc no_carry
    or al, 00000001b
no_carry:
    
    ; zero
    bt r15w, 6 +8
    jnc no_zero
    or al, 00000010b
no_zero:

    ; negative
    bt r15w, 7 +8
    jnc no_negative
    or al, 10000000b
no_negative:

    ; interrupt disable
    movzx rbx, byte ptr [rdx].state.flags_interruptDisable
    test bl, 1
    jz no_interrupt
    or al, 00000100b
no_interrupt:

    ; overflow
    movzx rbx, byte ptr [rdx].state.flags_overflow
    test bl, 1
    jz no_overflow
    or al, 01000000b
no_overflow:

    ; decimal
    movzx rbx, byte ptr [rdx].state.flags_decimal
    test bl, 1
    jz no_decimal
    or al, 00001000b
no_decimal:

    ; break
    movzx rbx, byte ptr [rdx].state.flags_break
    test bl, 1
    jz no_break
    or al, 00010000b
no_break:
    ; -------OK TO HERE
    mov byte ptr [rdi+10], al   ; falgs

    mov ax, word ptr [rdx].state.stackpointer
    mov byte ptr [rdi+11], al       ; SP
    pop rbx
    ;--------------------------- END DEBUG CAPTURE ------------------------
    add r11w, 1						; PC+1


    mov eax, 0ffffffffh
    mov [rdx].state.memory_write, eax
    mov [rdx].state.memory_read, eax
    mov [rdx].state.memory_readptr, eax

    ; Jump table
    lea rax, [instructions_table]				; start of jump table
    pushf
    add rax, [rax + rbx*8]
    popf
    jmp rax

cpu_is_waiting:
    add r14, 1
opcode_done::

    mov rcx, [rdx].state.breakpoint_ptr
    pushf

    ; with these checks in one place we can implement read\write breakpoints

    mov eax, [rdx].state.memory_write
    cmp eax, 0ffffffffh
    je no_write
    or dword ptr [rcx + rax * 4], MEMORY_WRITE_VALUE
no_write:
    mov eax, [rdx].state.memory_read
    cmp eax, 0ffffffffh
    je no_read
    or dword ptr [rcx + rax * 4], MEMORY_READ

    mov eax, [rdx].state.memory_readptr
    cmp eax, 0ffffffffh
    je no_read
    or dword ptr [rcx + rax * 4], MEMORY_READ
no_read:

    popf
    mov rdi, [rdx].state.debug_pos

    call via_step	; todo: change to macro call

    ; ----------------------- AUDIO
    pushf
    mov rax, [rdx].state.clock_audionext    ; rax is a parameter to vera_render_audio
    cmp r14, rax
    jl no_vera_audio

    push rsi
    call vera_render_audio
    pop rsi

    no_vera_audio:

    mov rax, [rdx].state.clock_ymnext
    cmp r14, rax
    jl no_ym_audio

    call ym_render_audio
    mov eax, [rdx].state.ym_interrupt
    or [rdx].state.interrupt, al
    
    no_ym_audio:
    popf
    ; ----------------------- AUDIO DONE


    ; check for line irq (requires rbx to be the last cpu clock)
    mov rax, r14
    and rax, 0ffffffffffffff00h		; mask off lower bytes - 0xff * 3.125 is 800 dots.
    mov rbx, [rdx].state.last_cpulineclock
    cmp rax, rbx
    je main_loop

    mov [rdx].state.last_cpulineclock, rax			; store for next time

    mov rbx, [rdx].state.cpu_posy
    add rbx, 1
    cmp rbx, SCREEN_HEIGHT			; are we into the new frame?
    jl check_line_type		
    
    xor rbx, rbx					; if so, zero the current line
    mov [rdx].state.cpu_posy, rbx
    jmp line_check

check_line_type:
    ; set high bit in DC_Video for odd\even line, just flip per line.
    mov cl, byte ptr [rsi+CTRL]
    and cl, 7eh
    jnz not_dc_sel0

    mov cl, byte ptr [rsi+DC_VIDEO]
    xor cl, 10000000b
    mov byte ptr [rsi+DC_VIDEO], cl
not_dc_sel0:

    mov [rdx].state.cpu_posy, rbx
    cmp rbx, VBLANK
    jg main_loop
    je vsync
line_check:
    ;mov rsi, [rdx].state.memory_ptr
    ; check for line IRQ
    movzx rcx, byte ptr [rdx].state.interrupt_line
    test cl, cl
    jz main_loop
    
    mov cx, word ptr [rdx].state.interrupt_linenum
    cmp cx, bx
    jne main_loop

    ;mov cl, byte ptr [rdx].state.interrupt_line_hit
    ;test cl, cl
    ;jnz main_loop

    or byte ptr [rsi+ISR], 2							; set bit in memory
    mov byte ptr [rdx].state.interrupt_line_hit, 1		; record that its been hit
    mov byte ptr [rdx].state.interrupt, 1				; cpu interrupt

    jmp main_loop
vsync:
    ; only draw the screen if there is an update!
    movzx rax, byte ptr [rdx].state.display_dirty
    sub rax, 1
    ; comment this out to disable no display update optimisation
    ;js no_render_required

    mov byte ptr [rdx].state.display_dirty, al

    call vera_render_display

    mov [rdx].state.render_ready, 1						; signal that we need to redraw the UI
    clflushopt [rdx].state.render_ready
    jmp vera_render_done

no_render_required:
    mov byte ptr [rdx].state.display_dirty, 0

vera_render_done:
    add dword ptr [rdx].state.frame_count, 1

    mov eax, dword ptr [rdx].state.frame_control	; 0 for no control, 1 for wait every frame -- same as control
    test eax, eax
    jz in_warp

    pushf
    push rsi
    push r15
    push r14
    push r13
    push r12
    push r11
    push r10
    push r9
    push r8
    
    push rdx    ; we need rdx
    ;push rcx                                ; appears to be needed to add some space to the stack.
    call qword ptr [rdx].state.get_ticks    ; ticks to rax
    ;pop rcx
    pop rdx

    sub rax, [rdx].state.base_ticks         ; get host tick delta, this is milliseconds

    mov rbx, r14                            ; total cpu ticks
    shr rbx, 3                              ; / 8 (Mhz)

    imul rax, 1000                          ; host time * 1000
    sub rbx, rax                            ; rbx has the time to wait, in *microseconds*.
    jle no_cpu_wait                         ; we only wait for possitive values

    push rdx

    push rcx                                ; appears to be needed to add some space to the stack.
    mov rcx, rbx
    call qword ptr [rdx].state.sleep
    pop rcx

    pop rdx

no_cpu_wait:
    pop r8
    pop r9
    pop r10
    pop r11
    pop r12
    pop r13
    pop r14
    pop r15
    pop rsi
    popf

in_warp:
    ; todo: check if we need to do this, this should never really overflow
    ; Check if the cpu has gotten too high. if so then reset it.
;    mov rax, 08000000000000000h
;    test r14, rax
;    jz no_cpu_reset
;    mov rax, 07fffffffffffffffh
;    and r14, rax
;    mov [rdx].state.vera_clock, r14	; update vera clock as well.

no_cpu_reset:
    ; check and fire sprite collision IRQ
    ; if IRQ hit, bump display_dirty to ensure a re-render

    movzx rbx, byte ptr [rsi+ISR]
    and rbx, 0fh

    movzx rcx, byte ptr [rdx].state.interrupt_spcol
    test cl, cl
    jz vsync_test

    movzx rcx, byte ptr [rdx].state.frame_sprite_collision
    test rcx, rcx
    jz vsync_test

    shl rcx, 4
    or rcx, 4		; set spcol bit for ISR
    or rbx, rcx		; or on our new flags into ISR

    mov byte ptr [rsi+ISR], bl
    mov byte ptr [rdx].state.interrupt_spcol_hit, 1
    mov byte ptr [rdx].state.interrupt, 1
    mov byte ptr [rdx].state.display_dirty, 1

vsync_test:
    mov dword ptr [rdx].state.frame_sprite_collision, 0	; clear mask

    ; fire vsync IRQ
    movzx rcx, byte ptr [rdx].state.interrupt_vsync
    test cl, cl
    jz main_loop

    ; set vsync
    ; todo: use rbx from above??
    or byte ptr [rsi+ISR], 1
    mov byte ptr [rdx].state.interrupt_vsync_hit, 1
    mov byte ptr [rdx].state.interrupt, 1

    jmp main_loop

exit_loop:
    call vera_render_display
    mov [rdx].state.render_ready, 1						; signal that we need to redraw the UI
    clflushopt [rdx].state.render_ready
    
    ; return all ok
    write_state_obj

    push rdx
    ; dont need to save CPU registers, as they are now stored
    call qword ptr [rdx].state.get_ticks    ; ticks to rax
    pop rdx

    mov [rdx].state.clock_pause, rax

    mov eax, [rdx].state.exit_code

    restore_registers
    ;leave - masm adds this.
    ret

not_supported:
    mov rax, EXIT_NOTSUPPORTED
    ret

step_exit:
    call vera_render_display
    mov [rdx].state.render_ready, 1						; signal that we need to redraw the UI
    clflushopt [rdx].state.render_ready

    write_state_obj

    push rdx
    ; dont need to save CPU registers, as they are now stored
    call qword ptr [rdx].state.get_ticks    ; ticks to rax
    pop rdx

    mov [rdx].state.clock_pause, rax

    mov rax, EXIT_STEPPING    ; stepping

    restore_registers
    ;leave - masm adds this.
    ret

breakpoint_exit:
    call vera_render_display
    mov [rdx].state.render_ready, 1						; signal that we need to redraw the UI
    clflushopt [rdx].state.render_ready

    write_state_obj

    push rdx
    ; dont need to save CPU registers, as they are now stored
    call qword ptr [rdx].state.get_ticks    ; ticks to rax
    pop rdx

    mov [rdx].state.clock_pause, rax

    mov rax, EXIT_BREAKPOINT    ; breakpoint

    restore_registers
    ;leave - masm adds this.
    ret
asm_func ENDP

;
; Side effects macros
;

check_bank_switch macro
    local rambank_change, rombank_change, done, skip
    cmp rbx, 01h
    jg done
    jl rambank_change
rombank_change:
; rom bank is now 256 banks
;    movzx rax, byte ptr [rsi+1]	
;    and al, 1fh
;    mov byte ptr [rsi+1], al
;    cmp al, 1 --- ??

    call copy_rombank_to_memory

    jmp done
rambank_change:
    call switch_rambank

    done:
endm

; Check if we have read the vera data registers
check_vera_access macro check_allvera
    local done, vera_skip

    ;if check_allvera eq 1
        xor r13, r13
        lea rax, [rbx - (09f00h - 1)]		; set to bottom of range we're interested in
        cmp rax, 42h						; check upper bound of IO area + 1. Currently via1\2 + vera + YM
        cmovbe r13, rax						; set r13 to the address in vera + 1.
    ;else
    ;	lea rax, [rbx - 09f23h]				; get value to check
    ;	cmp rax, 1
    ;	setbe r13b							; store if we need to let vera know data has changed
    ;endif

done:

endm

; Expects r13b to be set only if one of the Data registers have been read from.
; also checks for rom\ram bank switches for writes
step_vera_read macro checkvera
    local skip
if checkvera eq 1
    test r13b, r13b
    jz skip
;	call vera_afterread
    call io_afterread

    skip:
endif	
endm

step_io_readwrite macro checkvera
    local skip
if checkvera eq 1
    test r13b, r13b
    jz skip
;	call vera_afterreadwrite
    call io_afterreadwrite

    skip:
endif
    check_bank_switch
endm

step_io_write macro checkvera
    local skip
if checkvera eq 1
    test r13b, r13b
    jz skip
    call io_afterwrite

    skip:
endif
    check_bank_switch
endm

; -----------------------------
; Read Only Memory / Vera Update
; -----------------------------
; PC should be correct, generally opcode timing -1

pre_write_check macro checkreadonly
    local no_vera_change
if checkreadonly eq 1
    cmp rbx, readonly_memory
    jg skip
    ; check vera write
    cmp r13, 21
    jl no_vera_change
    ; if vera changes, then update the display first
    mov byte ptr [rdx].state.display_dirty, 2 ; always draw two frames
    call vera_render_display
no_vera_change:
    mov r12b, byte ptr [rsi+rbx]	 ; store old value
endif
endm

; -----------------------------
; Read Memory
; -----------------------------

read_zp_rbx macro
    movzx rbx, byte ptr [rsi+r11]	; Get 8bit value in memory.
endm

read_zpx_rbx macro
    movzx rbx, byte ptr [rsi+r11]	; Get 8bit value in memory.
    add bl, r9b			; Add X
endm

read_zpy_rbx macro
    movzx rbx, byte ptr [rsi+r11]	; Get 8bit value in memory.
    add bl, r10b		; Add Y
endm

read_abs_rbx macro check_allvera
    movzx rbx, word ptr [rsi+r11]	; Get 16bit value in memory.
    check_vera_access check_allvera
endm

read_absx_rbx macro check_allvera
    movzx rbx, word ptr [rsi+r11]	; Get 16bit value in memory.
    add bx, r9w			; Add X
    check_vera_access check_allvera
endm

read_absx_rbx_pagepenalty macro
    local no_overflow
    movzx rbx, word ptr [rsi+r11]	; Get 16bit value in memory.
    add bl, r9b			; Add X
    jnc no_overflow
    add bh, 1			; Add high bit
    add r14, 1			; Add cycle penatly
no_overflow:
    check_vera_access 0
endm

read_absy_rbx macro check_allvera
    movzx rbx, word ptr [rsi+r11]	; Get 16bit value in memory.
    add bx, r10w		; Add Y
    check_vera_access check_allvera
endm

read_absy_rbx_pagepenalty macro
    local no_overflow
    movzx rbx, word ptr [rsi+r11]	; Get 16bit value in memory.
    add bl, r10b		; Add Y
    jnc no_overflow
    add bh, 1			; Add high bit
    add r14, 1			; Add cycle penatly
no_overflow:
    check_vera_access 0
endm

read_indx_rbx macro check_allvera
    movzx rbx, byte ptr [rsi+r11]	; Address in ZP
    add bl, r9b			; Add on X. Byte operation so it wraps.
    movzx rbx, word ptr [rsi+rbx]	; Address at location
    check_vera_access check_allvera
endm

read_ind_rbx macro check_allvera
    movzx rbx, word ptr [rsi+r11]	; Get 16bit value in memory.
    ;check_vera_access check_allvera	; Get value its pointing at
    push r11
    mov r11w, bx					; reads use r11, so save and copy value

    check_vera_access check_allvera
    pop r11
endm

read_indy_rbx_pagepenalty macro
    local no_overflow
    movzx rbx, byte ptr [rsi+r11]	; Address in ZP
    movzx rbx, word ptr [rsi+rbx]	; Address pointed at in ZP

    add bl, r10b		; Add Y to the lower address byte
    jnc no_overflow
    add bh, 1			; Inc higher address byte
    add r14, 1			; Add clock cycle
    clc

no_overflow:
    check_vera_access 0
endm

read_indy_rbx macro check_allvera
    local no_overflow
    movzx rbx, byte ptr [rsi+r11]	; Address in ZP
    movzx rbx, word ptr [rsi+rbx]	; Address pointed at in ZP
    add bx, r10w		            ; Add Y to the address
    check_vera_access check_allvera
endm

read_indzp_rbx macro check_allvera
    movzx rbx, byte ptr [rsi+r11]	; Address in ZP
    movzx rbx, word ptr [rsi+rbx]	; Address at location
    check_vera_access check_allvera
endm

; -----------------------------
; Flags
; -----------------------------

read_flags_rax macro
    mov eax, r15d	; move flags to rax
    sahf			; set eflags
endm

write_flags_r15 macro
    lahf			; move new flags to rax
    mov r15d, eax	; store
endm

; we dont use stc, as its actually slower!
write_flags_r15_preservecarry macro
    lahf						; move new flags to rax
    and r15d, 0100h				; preserve carry		
    or r15d, eax				; store flags over (carry is always clear)
endm

write_flags_r15_setnegative macro
    lahf						; move new flags to rax
    or eax, 1000000000000000b	; set negative flag
    mov r15d, eax				; store
endm

; -----------------------------
; Op Codes
; -----------------------------
; No need to increment PC for opcode


; -----------------------------
; LDA
; -----------------------------

lda_body_end macro checkvera, clock, pc
    test r8b, r8b
    lahf
    and r15w, 0100h			; preserve carry		
    or r15w, ax				; store flags over (carry is always clear)
    step_vera_read checkvera

    add r14, clock
    add r11w, pc

    jmp opcode_done
endm

lda_body macro checkvera, clock, pc
    mov [rdx].state.memory_read, ebx

    mov r8b, [rsi+rbx]
    lda_body_end checkvera, clock, pc
endm

xA9_lda_imm PROC
    mov	r8b, [rsi+r11]
    lda_body_end 0, 2, 1
xA9_lda_imm ENDP

xA5_lda_zp PROC
    read_zp_rbx
    lda_body 0, 3, 1
xA5_lda_zp ENDP

xB5_lda_zpx PROC
    read_zpx_rbx
    lda_body 0, 4, 1
xB5_lda_zpx endp

xAD_lda_abs proc
    read_abs_rbx 0
    lda_body 1, 4, 2
xAD_lda_abs endp

xBD_lda_absx proc
    read_absx_rbx_pagepenalty
    lda_body 1, 4, 2
xBD_lda_absx endp

xB9_lda_absy proc
    read_absy_rbx_pagepenalty
    lda_body 1, 4, 2
xB9_lda_absy endp

xA1_lda_indx proc
    read_indx_rbx 0
    lda_body 1 ,6, 1
xA1_lda_indx endp

xB1_lda_indy proc
    read_indy_rbx_pagepenalty
    lda_body 1, 5, 1
xB1_lda_indy endp

xB2_lda_indzp proc
    read_indzp_rbx 0
    lda_body 1, 5, 1
xB2_lda_indzp endp


; -----------------------------
; LDX
; -----------------------------

ldx_body_end macro checkvera, clock, pc
    test r9b, r9b
    write_flags_r15_preservecarry
    step_vera_read checkvera

    add r14, clock
    add r11w, pc

    jmp opcode_done
endm

ldx_body macro checkvera, clock, pc
    mov [rdx].state.memory_read, ebx
  
    mov r9b, [rsi+rbx]
    ldx_body_end checkvera, clock, pc
endm

xA2_ldx_imm PROC
    mov	r9b, [rsi+r11]
    ldx_body_end 0, 2, 1
xA2_ldx_imm ENDP

xA6_ldx_zp PROC
    read_zp_rbx
    ldx_body 0, 3, 1
xA6_ldx_zp  ENDP

xB6_ldx_zpy PROC
    read_zpy_rbx
    ldx_body 0, 4, 1
xB6_ldx_zpy endp

xAE_ldx_abs proc
    read_abs_rbx 0
    ldx_body 1, 4, 2
xAE_ldx_abs endp

xBE_ldx_absy proc
    read_absy_rbx_pagepenalty
    ldx_body 1, 4, 2
xBE_ldx_absy endp

; -----------------------------
; LDY
; -----------------------------

ldy_body_end macro checkvera, clock, pc
    test r10b, r10b
    write_flags_r15_preservecarry
    step_vera_read checkvera

    add r14, clock
    add r11w, pc

    jmp opcode_done
endm

ldy_body macro checkvera, clock, pc
    mov [rdx].state.memory_read, ebx 

    mov r10b, [rsi+rbx]
    ldy_body_end checkvera, clock, pc
endm

xA0_ldy_imm PROC
    mov	r10b, [rsi+r11]
    ldy_body_end 0, 2, 1
xA0_ldy_imm ENDP

xA4_ldy_zp PROC
    read_zp_rbx
    ldy_body 0, 3, 1
xA4_ldy_zp  ENDP

xB4_ldy_zpx PROC
    read_zpx_rbx
    ldy_body 0, 4, 1
xB4_ldy_zpx endp

xAC_ldy_abs proc
    read_abs_rbx 0
    ldy_body 1, 4, 2
xAC_ldy_abs endp

xBC_ldy_absx proc
    read_absx_rbx_pagepenalty
    ldy_body 1, 4, 2
xBC_ldy_absx endp


; -----------------------------
; STA
; -----------------------------

sta_body macro checkvera, checkreadonly, clock, pc
    pre_write_check checkreadonly

    mov byte ptr [rsi+rbx], r8b
    mov [rdx].state.memory_write, ebx

    step_io_write checkvera

skip:
    add r14, clock
    add r11w, pc			; add on PC

    jmp opcode_done
endm

x85_sta_zp proc	
    read_zp_rbx
    sta_body 0, 0, 3, 1
x85_sta_zp endp

x95_sta_zpx proc
    read_zpx_rbx
    sta_body 0, 0, 4, 1
x95_sta_zpx endp

x8D_sta_abs proc
    read_abs_rbx 1
    sta_body 1, 1, 4, 2
x8D_sta_abs endp

x9D_sta_absx proc
    read_absx_rbx 1
    sta_body 1, 1, 5, 2
x9D_sta_absx endp

x99_sta_absy proc
    read_absy_rbx 1
    sta_body 1, 1, 5, 2
x99_sta_absy endp

x81_sta_indx proc
    read_indx_rbx 1
    sta_body 1, 1, 6, 1
x81_sta_indx endp

x91_sta_indy proc
    read_indy_rbx 1
    sta_body 1, 1, 6, 1
x91_sta_indy endp

x92_sta_indzp proc
    read_indzp_rbx 1
    sta_body 1, 1, 5, 1
x92_sta_indzp endp

;
; STX
;

stx_body macro checkvera, checkreadonly, clock, pc
    pre_write_check checkreadonly

    mov byte ptr [rsi+rbx], r9b
    mov [rdx].state.memory_write, ebx

    step_io_write checkvera
    
skip:
    add r14, clock
    add r11w, pc			; add on PC

    jmp opcode_done
endm

x86_stx_zp proc
    read_zp_rbx
    stx_body 0, 0, 3, 1
x86_stx_zp endp

x96_stx_zpy proc
    read_zpy_rbx
    stx_body 0, 0, 4, 1
x96_stx_zpy endp

x8E_stx_abs proc
    read_abs_rbx 1
    stx_body 1, 1, 4, 2
x8E_stx_abs endp

;
; STY
;

sty_body macro checkvera, checkreadonly, clock, pc
    pre_write_check checkreadonly

    mov byte ptr [rsi+rbx], r10b
    mov [rdx].state.memory_write, ebx

    step_io_write checkvera
    
skip:
    add r14, clock
    add r11w, pc			; add on PC

    jmp opcode_done
endm

x84_sty_zp proc
    read_zp_rbx
    sty_body 0, 0, 3, 1
x84_sty_zp endp

x94_sty_zpx proc
    read_zpx_rbx
    sty_body 0, 0, 4, 1
x94_sty_zpx endp

x8C_sty_abs proc
    read_abs_rbx 1
    sty_body 1, 1, 4, 2
x8C_sty_abs endp

;
; STZ
;

stz_body macro checkvera, checkreadonly, clock, pc
    pre_write_check checkreadonly

    mov byte ptr [rsi+rbx], 0
    mov [rdx].state.memory_write, ebx

    step_io_write checkvera

skip:
    add r14, clock
    add r11w, pc			; add on PC

    jmp opcode_done
endm

x64_stz_zp proc
    read_zp_rbx
    stz_body 0, 0, 3, 1
x64_stz_zp endp

x74_stz_zpx proc
    read_zpx_rbx
    stz_body 0, 0, 4, 1
x74_stz_zpx endp

x9C_stz_abs proc
    read_abs_rbx 1
    stz_body 1, 1, 4, 2
x9C_stz_abs endp

x9E_stz_absx proc
    read_absx_rbx 1
    stz_body 1, 1, 5, 2
x9E_stz_absx endp

;
; INC\DEC
;

inc_body macro checkvera, checkreadonly, clock, pc
    pre_write_check checkreadonly

    clc
    inc byte ptr [rsi+rbx]

    write_flags_r15_preservecarry

    mov [rdx].state.memory_read, ebx
    mov [rdx].state.memory_write, ebx

    step_io_readwrite checkvera

skip:
    add r14, clock
    add r11w, pc			; add on PC

    jmp opcode_done
endm

dec_body macro checkvera, checkreadonly, clock, pc
    pre_write_check checkreadonly

    clc
    dec byte ptr [rsi+rbx]

    write_flags_r15_preservecarry

    mov [rdx].state.memory_read, ebx
    mov [rdx].state.memory_write, ebx

    step_io_readwrite checkvera

skip:
    add r14, clock
    add r11w, pc			; add on PC

    jmp opcode_done
endm

x1A_inc_a proc
    inc r8b
    write_flags_r15_preservecarry

    add r14, 2
    jmp opcode_done
x1A_inc_a endp

x3A_dec_a proc
    dec r8b
    write_flags_r15_preservecarry

    add r14, 2
    jmp opcode_done
x3A_dec_a endp


xEE_inc_abs proc
    read_abs_rbx 0
    inc_body 1, 1, 6, 2
xEE_inc_abs endp

xCE_dec_abs proc
    read_abs_rbx 0
    dec_body 1, 1, 6, 2
xCE_dec_abs endp


xFE_inc_absx proc
    read_absx_rbx 0
    inc_body 1, 1, 7, 2
xFE_inc_absx endp

xDE_dec_absx proc
    read_absx_rbx 0
    dec_body 1, 1, 7, 2
xDE_dec_absx endp


xE6_inc_zp proc
    read_zp_rbx
    inc_body 0, 0, 5, 1
xE6_inc_zp endp

xC6_dec_zp proc
    read_zp_rbx
    dec_body 0, 0, 5, 1
xC6_dec_zp endp


xF6_inc_zpx proc
    read_zpx_rbx
    inc_body 0, 0, 6, 1
xF6_inc_zpx endp

xD6_dec_zpx proc
    read_zpx_rbx
    dec_body 0, 0, 6, 1
xD6_dec_zpx endp

;
; INX\DEX
;

xE8_inx proc
    inc r9b
    write_flags_r15_preservecarry

    add r14, 2
    jmp opcode_done
xE8_inx endp

xCA_dex proc

    dec r9b
    write_flags_r15_preservecarry

    add r14, 2
    jmp opcode_done
xCA_dex endp

;
; INY\DEY
;

xC8_iny proc
    inc r10b
    write_flags_r15_preservecarry

    add r14, 2
    jmp opcode_done
xC8_iny endp

x88_dey proc
    dec r10b
    write_flags_r15_preservecarry

    add r14, 2
    jmp opcode_done
x88_dey endp

;
; Register Transfer
;

xAA_tax proc
    add r14, 2
    mov	r9, r8		; A -> X

    test r9b, r9b
    write_flags_r15_preservecarry

    jmp opcode_done
xAA_tax endp

x8A_txa proc
    add r14, 2
    mov	r8, r9		; X -> A

    test r8b, r8b
    write_flags_r15_preservecarry

    jmp opcode_done
x8A_txa endp

xA8_tay proc
    add r14, 2
    mov	r10, r8		; A -> Y

    test r10b, r10b
    write_flags_r15_preservecarry

    jmp opcode_done
xA8_tay endp

x98_tya proc
    add r14, 2
    mov	r8, r10		; Y -> A

    test r8b, r8b
    write_flags_r15_preservecarry

    jmp opcode_done
x98_tya endp

;
; Shifts
;

;
; ASL
;

asl_body macro checkreadonly, clock, pc
    pre_write_check checkreadonly

    read_flags_rax
    sal byte ptr [rsi+rbx],1		; shift

    write_flags_r15	

    mov [rdx].state.memory_read, ebx
    mov [rdx].state.memory_write, ebx
    
    add r11w, pc					; move PC on
    add r14, clock					; Clock

    jmp opcode_done	

if checkreadonly eq 1
skip:

    read_flags_rax
    movzx r12, byte ptr [rsi+rbx]
    sal r12b, 1						; shift

    write_flags_r15	

    add r11w, pc					; move PC on
    add r14, clock					; Clock

    jmp opcode_done	
endif
endm

x0A_asl_a proc
    read_flags_rax
    sal r8b, 1		; shift
    write_flags_r15

    add r14, 2		; Clock

    jmp opcode_done	
x0A_asl_a endp

x0E_asl_abs proc
    read_abs_rbx 0
    asl_body 1, 6, 2
x0E_asl_abs endp

x1E_asl_absx proc
    read_absx_rbx 0
    asl_body 1, 7, 2
x1E_asl_absx endp

x06_asl_zp proc
    read_zp_rbx
    asl_body 0, 5, 1
x06_asl_zp endp

x16_asl_zpx proc
    read_zpx_rbx
    asl_body 0, 6, 1
x16_asl_zpx endp

;
; LSR
;

lsr_body macro checkreadonly, clock, pc
    pre_write_check checkreadonly

    movzx r12, byte ptr [rsi+rbx]
    shr r12b,1	; shift
    mov byte ptr [rsi+rbx], r12b

    write_flags_r15	

    mov [rdx].state.memory_read, ebx
    mov [rdx].state.memory_write, ebx

    add r14, clock				; Clock
    add r11w, pc				; add on PC

    jmp opcode_done	

if checkreadonly eq 1
skip:
    movzx r12, byte ptr [rsi+rbx]
    shr r12b,1					; shift

    write_flags_r15	

    add r14, clock				; Clock
    add r11w, pc				; add on PC

    jmp opcode_done	
endif
endm

x4A_lsr_a proc
    shr r8b,1		; shift

    write_flags_r15

    add r14, 2		; Clock

    jmp opcode_done	
x4A_lsr_a endp

x4E_lsr_abs proc
    read_abs_rbx 0
    lsr_body 1, 6, 2
x4E_lsr_abs endp

x5E_lsr_absx proc
    read_absx_rbx 0
    lsr_body 1, 7, 2
x5E_lsr_absx endp

x46_lsr_zp proc
    read_zp_rbx
    lsr_body 0, 5, 1
x46_lsr_zp endp

x56_lsr_zpx proc
    read_zpx_rbx
    lsr_body 0, 6, 1
x56_lsr_zpx endp

;
; ROL
;

rol_body macro checkreadonly, clock, pc
    pre_write_check checkreadonly

    mov rdi, r15					; save registers
    and rdi, 0100h					; mask carry
    ror rdi, 8						; move to lower byte

    ;        NZ A P C
    mov r12, 0100000000000000b 
    xor r13, r13

    sal byte ptr [rsi+rbx], 1		; shift
    write_flags_r15
    or byte ptr [rsi+rbx], dil		; add carry on

    cmovnz r12, r13
    and r15, 1011111111111111b		; mask off the zero flag
    or r15, r12						; add on zero flag if needed

    mov [rdx].state.memory_read, ebx
    mov [rdx].state.memory_write, ebx
    
    add r14, clock					; Clock
    add r11w, pc					; add on PC
    jmp opcode_done	

if checkreadonly eq 1
skip:

    mov rdi, r15					; save registers
    and rdi, 0100h					; mask carry
    ror rdi, 8						; move to lower byte

    movzx r12, byte ptr [rsi+rbx]
    sal r12b, 1						; shift
    write_flags_r15
    
    add r14, clock					; Clock
    add r11w, pc					; add on PC
    jmp opcode_done	
endif
endm

x2A_rol_a proc
    mov rdi, r15					; save registers
    and rdi, 0100h					; mask carry
    ror rdi, 8						; move to lower byte

    ;        NZ A P C
    mov r12, 0100000000000000b 
    xor r13, r13

    sal r8b,1						; shift
    write_flags_r15
    or r8b, dil						; add carry on
    
    cmovnz r12, r13
    and r15, 1011111111111111b		; mask off the zero flag
    or r15, r12						; add on zero flag if needed
    
    add r14, 2						; Clock
    jmp opcode_done	
x2A_rol_a endp

x2E_rol_abs proc	
    read_abs_rbx 0
    rol_body 1, 6, 2
x2E_rol_abs endp

x3E_rol_absx proc	
    read_absx_rbx 0
    rol_body 1, 7, 2
x3E_rol_absx endp

x26_rol_zp proc
    read_zp_rbx
    rol_body 0, 5, 1
x26_rol_zp endp

x36_rol_zpx proc
    read_zpx_rbx
    rol_body 0, 6, 1
x36_rol_zpx endp

;
; ROR
;

ror_body macro checkreadonly, clock, pc
    pre_write_check checkreadonly

    mov rdi, r15					; save registers
    and rdi, 0100h					; mask carry
    ror rdi, 1						; move to high bit on lower byte
    
    ;        NZ A P C
    mov r12, 0100000000000000b 
    xor r13, r13

    shr byte ptr [rsi+rbx], 1		; shift
    write_flags_r15
    or byte ptr [rsi+rbx], dil		; add carry on
      
    cmovnz r12, r13
    and r15, 1011111111111111b		; mask off the zero flag
    or r15, r12						; add on zero flag if needed
    
    rol rdi, 8						; change carry to negative
    or r15, rdi						; add on to flags
  
    mov [rdx].state.memory_read, ebx
    mov [rdx].state.memory_write, ebx

    add r14, clock					; Clock
    add r11w, pc					; add on PC
    jmp opcode_done	

if checkreadonly eq 1
skip:

    mov rdi, r15					; save registers
    and rdi, 0100h					; mask carry
    ror rdi, 1						; move to high bit on lower byte

    movzx r12, byte ptr [rsi+rbx]
    shr r12b, 1						; shift
    write_flags_r15

    rol rdi, 8						; change carry to negative
    or r15, rdi						; add on to flags

    add r14, clock					; Clock
    add r11w, pc					; add on PC
    jmp opcode_done	
endif
endm

x6A_ror_a proc
    mov rdi, r15					; save registers
    and rdi, 0100h					; mask carry
    ror rdi, 1						; move to high bit on lower byte

    ;        NZ A P C
    mov r12, 0100000000000000b 
    xor r13, r13

    shr r8b,1						; shift
    write_flags_r15
    or r8b, dil						; add carry on
    
    cmovnz r12, r13
    and r15, 1011111111111111b		; mask off the zero flag
    or r15, r12						; add on zero flag if needed
    

    rol rdi, 8						; change carry to negative
    or r15, rdi						; add on to flags

    add r14, 2						; Clock
    jmp opcode_done	
x6A_ror_a endp

x6E_ror_abs proc	
    read_abs_rbx 0
    ror_body 1, 6, 2
x6E_ror_abs endp

x7E_ror_absx proc	
    read_absx_rbx 0
    ror_body 1, 7, 2
x7E_ror_absx endp

x66_ror_zp proc
    read_zp_rbx
    ror_body 0, 5, 1
x66_ror_zp endp

x76_ror_zpx proc
    read_zpx_rbx
    ror_body 0, 6, 1
x76_ror_zpx endp

;
; AND
;

and_body_end macro checkvera, clock, pc
    and r8b, [rsi+rbx]

    write_flags_r15_preservecarry
    step_vera_read checkvera

    add r14, clock		; Clock
    add r11w, pc			; add on PC
    jmp opcode_done	
endm

x29_and_imm proc
    and r8b, [rsi+r11]
    write_flags_r15_preservecarry

    add r14, 2		; Clock
    add r11w, 1		; PC
    jmp opcode_done	
x29_and_imm endp

x2D_and_abs proc
    read_abs_rbx 0
    and_body_end 1, 4, 2
x2D_and_abs endp

x3D_and_absx proc
    read_absx_rbx_pagepenalty
    and_body_end 1, 4, 2
x3D_and_absx endp

x39_and_absy proc
    read_absy_rbx_pagepenalty
    and_body_end 1, 4, 2
x39_and_absy endp

x25_and_zp proc
    read_zp_rbx
    and_body_end 0, 3, 1
x25_and_zp endp

x35_and_zpx proc
    read_zpx_rbx
    and_body_end 0, 4, 1
x35_and_zpx endp

x32_and_indzp proc
    read_indzp_rbx 0
    and_body_end 1, 5, 1
x32_and_indzp endp

x21_and_indx proc
    read_indx_rbx 0
    and_body_end 1, 6, 1
x21_and_indx endp

x31_and_indy proc
    read_indy_rbx_pagepenalty
    and_body_end 1, 5, 1
x31_and_indy endp

;
; EOR
;

eor_body_end macro checkvera, clock, pc
    xor r8b, [rsi+rbx]
    write_flags_r15_preservecarry
    step_vera_read checkvera

    add r14, clock	
    add r11w, pc

    jmp opcode_done	
endm

x49_eor_imm proc
    xor r8b, [rsi+r11]
    write_flags_r15_preservecarry

    add r14, 2		; Clock
    add r11w, 1		; PC

    jmp opcode_done	
x49_eor_imm endp

x4D_eor_abs proc
    read_abs_rbx 0
    eor_body_end 1, 4, 2
x4D_eor_abs endp

x5D_eor_absx proc
    read_absx_rbx_pagepenalty
    eor_body_end 1, 4, 2
x5D_eor_absx endp

x59_eor_absy proc
    read_absy_rbx_pagepenalty
    eor_body_end 1, 4, 2
x59_eor_absy endp

x45_eor_zp proc
    read_zp_rbx
    eor_body_end 0, 3, 1
x45_eor_zp endp

x55_eor_zpx proc
    read_zpx_rbx
    eor_body_end 0, 4, 1
x55_eor_zpx endp

x52_eor_indzp proc
    read_indzp_rbx 0
    eor_body_end 1, 5, 1
x52_eor_indzp endp

x41_eor_indx proc
    read_indx_rbx 0
    eor_body_end 1, 6, 1
x41_eor_indx endp

x51_eor_indy proc
    read_indy_rbx_pagepenalty
    eor_body_end 1, 5, 1
x51_eor_indy endp

;
; OR
;

ora_body macro checkvera, clock, pc
    or r8b, [rsi+rbx]
    write_flags_r15_preservecarry
    step_vera_read checkvera
    
    add r11w, pc		; add on PC
    add r14, clock		; Clock
    jmp opcode_done	
endm

x09_ora_imm proc
    or r8b, [rsi+r11]
    write_flags_r15_preservecarry

    add r11w, 1		; PC
    add r14, 2		; Clock
    jmp opcode_done	
x09_ora_imm endp

x0D_ora_abs proc
    read_abs_rbx 0
    ora_body 1, 4, 2
x0D_ora_abs endp

x1D_ora_absx proc
    read_absx_rbx_pagepenalty
    ora_body 1 ,4, 2
x1D_ora_absx endp

x19_ora_absy proc
    read_absy_rbx_pagepenalty
    ora_body 1, 4, 2
x19_ora_absy endp

x05_ora_zp proc
    read_zp_rbx
    ora_body 0, 3, 1
x05_ora_zp endp

x15_ora_zpx proc
    read_zpx_rbx
    ora_body 0, 4, 1
x15_ora_zpx endp

x12_ora_indzp proc
    read_indzp_rbx 0
    ora_body 1 ,5, 1
x12_ora_indzp endp

x01_ora_indx proc
    read_indx_rbx 0
    ora_body 1, 6, 1
x01_ora_indx endp

x11_ora_indy proc
    read_indy_rbx_pagepenalty
    ora_body 1, 5, 1
x11_ora_indy endp

;
; ADC
;
adc_body_end macro checkvera, clock, pc, preservecarry
    if preservecarry eq 0
        write_flags_r15
    endif
    if preservecarry eq 1
        write_flags_r15_preservecarry
    endif

    seto dil
    mov byte ptr [rdx].state.flags_overflow, dil
    step_vera_read checkvera

    add r14, clock			; Clock
    add r11w, pc			; add on PC
    jmp opcode_done	
endm

decimal_add macro imm
    local no_decimal_overflow, no_total_overflow

    mov r12, r8         
    if imm eq 0
        movzx rcx, byte ptr [rsi+rbx]
    endif
    if imm eq 1
        movzx rcx, byte ptr [rsi+r11] 
    endif

    ; flag set for C, ecx = Data, r12 = A, will total into ecx

    ; Add (A & 0x0f) + (Data & 0x0f) + C
    and ecx, 00fh
    and r12d, 00fh

    ; set the flags, so set the Carry flag
    read_flags_rax
    adc rcx, r12    ; C + Data + A

    cmp ecx, 00ah
    jl no_decimal_overflow

    add ecx, 006h
    and ecx, 00fh
    add ecx, 010h

no_decimal_overflow:

    mov r12, r8         
    if imm eq 0
        movzx rax, byte ptr [rsi+rbx]
    endif
    if imm eq 1
        movzx rax, byte ptr [rsi+r11] 
    endif   

    and eax, 0f0h
    and r12, 0f0h
    
    add ecx, eax
    add ecx, r12d

    cmp ecx, 0a0h
    jl no_total_overflow

    add ecx, 060h

no_total_overflow:

    mov r8b, cl

    ; Z set if CL is zero
    ; N if bit 7 is set
    ; V as normal?
    ; C if rcx > 0x100
    
    ; so test ecx for >= 0x100, and set the carry (r15), the macro will pick the rest up after
    and r15w, 0feffh				; mask carry	
    xor eax, eax
    cmp ecx, 0100h          
    setge al                    ; set if above
    shl rax, 8
    or r15, rax                 ; or in

    add r8b, 0                  ; sets flags
endm

adc_body macro checkvera, clock, pc
    local decimal
    movzx rax, byte ptr [rdx].state.flags_decimal
    test rax, rax
    jnz decimal
    read_flags_rax

    adc r8b, [rsi+rbx]

    adc_body_end checkvera, clock, pc, 0

    decimal:

    decimal_add 0
    adc_body_end checkvera, clock, pc, 1
endm

x69_adc_imm proc
    movzx rax, byte ptr [rdx].state.flags_decimal
    test rax, rax
    jnz decimal
    read_flags_rax

    adc r8b, [rsi+r11]

    adc_body_end 0, 2, 1, 0

    decimal:

    decimal_add 1
    adc_body_end 0, 2, 1, 1
x69_adc_imm endp

x6D_adc_abs proc
    read_abs_rbx 0
    adc_body 1, 4, 2
x6D_adc_abs endp

x7D_adc_absx proc
    read_absx_rbx_pagepenalty
    adc_body 1, 4, 2
x7D_adc_absx endp

x79_adc_absy proc
    read_absy_rbx_pagepenalty
    adc_body 1, 4, 2
x79_adc_absy endp

x65_adc_zp proc
    read_zp_rbx
    adc_body 0, 3, 1
x65_adc_zp endp

x75_adc_zpx proc
    read_zpx_rbx
    adc_body 0, 4, 1
x75_adc_zpx endp

x72_adc_indzp proc
    read_indzp_rbx 0
    adc_body 1, 5, 1
x72_adc_indzp endp

x61_adc_indx proc
    read_indx_rbx 0
    adc_body 1, 6, 1
x61_adc_indx endp

x71_adc_indy proc
    read_indy_rbx_pagepenalty
    adc_body 1, 5, 1
x71_adc_indy endp

;
; SBC
;

sbc_body_end macro checkvera, clock, pc, preservecarry
    if preservecarry eq 0
        write_flags_r15
    endif
    if preservecarry eq 1
        write_flags_r15_preservecarry
    endif

    seto dil
    mov byte ptr [rdx].state.flags_overflow, dil
    step_vera_read checkvera

    add r14, clock			; Clock
    add r11w, pc			; add on PC
    jmp opcode_done	
endm

decimal_sub macro imm
    local no_decimal_overflow, no_total_overflow

    mov rcx, r8         
    if imm eq 0
        movzx r12, byte ptr [rsi+rbx]
    endif
    if imm eq 1
        movzx r12, byte ptr [rsi+r11] 
    endif

    ; flag set for C, ecx = A, r12 = Data, will total into ecx

    ; Add (A & 0x0f) + (Data & 0x0f) + C
    and ecx, 00fh
    and r12d, 00fh

    ; set the flags, so set the Carry flag
    read_flags_rax
   ; int 3
    cmc
    sbb rcx, r12    ; Data - A + (C-1), this is the only point C is used

    cmp ecx, 00h
    jge no_decimal_overflow

    sub ecx, 006h
    and ecx, 00fh
    sub ecx, 010h

no_decimal_overflow:

    mov r12, r8         
    if imm eq 0
        movzx rax, byte ptr [rsi+rbx]
    endif
    if imm eq 1
        movzx rax, byte ptr [rsi+r11] 
    endif

    and eax, 0f0h
    and r12d, 0f0h
    
    sub r12d, eax

    add ecx, r12d ; this is a or...?
;    sub ecx, r12d

    cmp ecx, 0h
    jge no_total_overflow

    sub ecx, 060h

no_total_overflow:

    mov r8b, cl

    ; Z set if CL is zero
    ; N if bit 7 is set
    ; V as normal?
    ; C if rcx > 0x100
    
    ; so test ecx for >= 0x100, and set the carry (r15), the macro will pick the rest up after
    and r15w, 0feffh				; mask carry	
    xor eax, eax
    cmp ecx, 0000h          
    setge al                    ; set if above
    shl rax, 8
    or r15, rax                 ; or in

    add r8b, 0                  ; sets flags
endm

sbc_body macro checkvera, clock, pc
    local decimal
    movzx rax, byte ptr [rdx].state.flags_decimal
    test rax, rax
    jnz decimal
    read_flags_rax

    cmc
    sbb r8b, [rsi+rbx]
    cmc

    sbc_body_end checkvera, clock, pc, 0

    decimal:

    decimal_sub 0
    sbc_body_end checkvera, clock, pc, 1
endm

xE9_sbc_imm proc
    movzx rax, byte ptr [rdx].state.flags_decimal
    test rax, rax
    jnz decimal
    read_flags_rax

    cmc
    sbb r8b, [rsi+r11]
    cmc

    sbc_body_end 0, 2, 1, 0

    decimal:

    decimal_sub 1
    sbc_body_end 0, 2, 1, 1
xE9_sbc_imm endp

xED_sbc_abs proc
    read_abs_rbx 0
    sbc_body 1, 4, 2
xED_sbc_abs endp

xFD_sbc_absx proc
    read_absx_rbx_pagepenalty
    sbc_body 1, 4, 2
xFD_sbc_absx endp

xF9_sbc_absy proc
    read_absy_rbx_pagepenalty
    sbc_body 1, 4, 2
xF9_sbc_absy endp

xE5_sbc_zp proc
    read_zp_rbx
    sbc_body 0, 3, 1
xE5_sbc_zp endp

xF5_sbc_zpx proc
    read_zpx_rbx
    sbc_body 0, 4, 1
xF5_sbc_zpx endp

xF2_sbc_indzp proc
    read_indzp_rbx 0
    sbc_body 1, 5, 1
xF2_sbc_indzp endp

xE1_sbc_indx proc
    read_indx_rbx 0
    sbc_body 1, 6, 1
xE1_sbc_indx endp

xF1_sbc_indy proc
    read_indy_rbx_pagepenalty
    sbc_body 1, 5, 1
xF1_sbc_indy endp

;
; CMP
;

cmp_body_end macro checkvera, clock, pc
    cmc
    write_flags_r15
    step_vera_read checkvera

    add r14, clock			; Clock
    add r11w, pc			; add on PC
    jmp opcode_done	
endm

cmp_body macro checkvera, clock, pc
    cmp r8b, [rsi+rbx]
    cmp_body_end checkvera, clock, pc
endm

xC9_cmp_imm proc
    cmp r8b, [rsi+r11]		
    cmp_body_end 0, 2, 1
xC9_cmp_imm endp

xCD_cmp_abs proc
    read_abs_rbx 0
    cmp_body 1, 4, 2
xCD_cmp_abs endp

xDD_cmp_absx proc
    read_absx_rbx_pagepenalty
    cmp_body 1, 4, 2
xDD_cmp_absx endp

xD9_cmp_absy proc
    read_absy_rbx_pagepenalty
    cmp_body 1, 4, 2
xD9_cmp_absy endp

xC5_cmp_zp proc
    read_zp_rbx
    cmp_body 0, 3, 1
xC5_cmp_zp endp

xD5_cmp_zpx proc
    read_zpx_rbx
    cmp_body 0, 4, 1
xD5_cmp_zpx endp

xD2_cmp_indzp proc
    read_indzp_rbx 0
    cmp_body 1, 5, 1
xD2_cmp_indzp endp

xC1_sbc_indx proc
    read_indx_rbx 0
    cmp_body 1, 6, 1
xC1_sbc_indx endp

xD1_cmp_indy proc
    read_indy_rbx_pagepenalty
    cmp_body 1, 5, 1
xD1_cmp_indy endp

;
; CMPX
;

cmpx_body macro checkvera, clock, pc
    cmp r9b, [rsi+rbx]
    cmp_body_end checkvera, clock, pc
endm

xE0_cmpx_imm proc
    cmp r9b, [rsi+r11]		
    cmp_body_end 0, 2, 1
xE0_cmpx_imm endp

xEC_cmpx_abs proc
    read_abs_rbx 0
    cmpx_body 1, 4, 2
xEC_cmpx_abs endp

xE4_cmpx_zp proc
    read_zp_rbx
    cmpx_body 0, 3, 1
xE4_cmpx_zp endp

;
; CMPY
;

cmpy_body macro checkvera, clock, pc
    cmp r10b, [rsi+rbx]
    cmp_body_end checkvera, clock, pc
endm

xC0_cmpy_imm proc
    cmp r10b, [rsi+r11]		
    cmp_body_end 0, 2, 1
xC0_cmpy_imm endp

xCC_cmpy_abs proc
    read_abs_rbx 0
    cmpy_body 1, 4, 2
xCC_cmpy_abs endp

xC4_cmpy_zp proc
    read_zp_rbx
    cmpy_body 0, 3, 1
xC4_cmpy_zp endp

;
; Flag Modifiers
;

x18_clc proc
    ;                |
    and r15w, 1111111011111111b

    add r14, 2			; Clock
    jmp opcode_done	
x18_clc endp

x38_sec proc
    ;                |
    or r15w, 0000000100000000b

    add r14, 2			; Clock
    jmp opcode_done	
x38_sec endp

xD8_cld proc
    mov byte ptr [rdx].state.flags_decimal, 0
    add r14, 2			; Clock
    jmp opcode_done	
xD8_cld endp

xF8_sed proc
    mov byte ptr [rdx].state.flags_decimal, 1
    add r14, 2			; Clock
    jmp opcode_done	
xF8_sed endp

x58_cli proc
    mov byte ptr [rdx].state.flags_interruptDisable, 0
    add r14, 2			; Clock
    jmp opcode_done	
x58_cli endp

x78_sei proc
    mov byte ptr [rdx].state.flags_interruptDisable, 1
    add r14, 2			; Clock
    jmp opcode_done	
x78_sei endp

xB8_clv proc
    mov byte ptr [rdx].state.flags_overflow, 0
    add r14, 2			; Clock
    jmp opcode_done	
xB8_clv endp

;
; Branches
;

bra_perform_jump macro
    local page_change

    movsx bx, byte ptr [rsi+r11]	; Get value at PC and turn it into a 2byte signed value
    add r11w, 1						; move PC on -- all jumps are relative
    mov rax, r11					; store PC
    add r11w, bx
    
    mov rbx, r11
    cmp ah, bh						; test if the page has changed.
    jne page_change

    add r14, 3						; Clock

    jmp opcode_done	

page_change:						; page change as a 1 cycle penalty
    add r14, 4						; Clock
    jmp opcode_done

endm

bra_nojump macro
    add r14, 2			; Clock
    add r11w, 1			; move PC on

    jmp opcode_done	
endm

x80_bra proc
    bra_perform_jump
x80_bra endp

xD0_bne proc
    mov rax, r15	; move flags to rax
    sahf			; set eflags

    jnz branch
    bra_nojump

branch:
    bra_perform_jump

xD0_bne endp

xF0_beq proc
    mov rax, r15	; move flags to rax
    sahf			; set eflags

    jz branch
    bra_nojump

branch:
    bra_perform_jump
xF0_beq endp

x10_bpl proc
    mov rax, r15	; move flags to rax
    sahf			; set eflags

    jns branch
    bra_nojump

branch:
    bra_perform_jump
x10_bpl endp

x30_bmi proc
    mov rax, r15	; move flags to rax
    sahf			; set eflags

    js branch
    bra_nojump

branch:
    bra_perform_jump
x30_bmi endp

x90_bcc proc
    mov rax, r15	; move flags to rax
    sahf			; set eflags

    jnc branch
    bra_nojump

branch:
    bra_perform_jump
x90_bcc endp

xB0_bcs proc
    mov rax, r15	; move flags to rax
    sahf			; set eflags

    jc branch
    bra_nojump

branch:
    bra_perform_jump
xB0_bcs endp

x50_bvc proc
    movzx rax, byte ptr [rdx].state.flags_overflow
    test al, al
    jz branch
    bra_nojump

branch:
    bra_perform_jump
x50_bvc endp

x70_bvs proc
    movzx rax, byte ptr [rdx].state.flags_overflow
    test al, al
    jnz branch
    bra_nojump

branch:
    bra_perform_jump
x70_bvs endp

;
; BBR
;

bb_perform_jump macro
    local page_change

    movsx bx, byte ptr [rsi+r11+1]	; Get value at PC+1 and turn it into a 2byte signed value
    add r11w, 2						; move PC on -- all jumps are relative
    mov rax, r11					; store PC
    add r11w, bx
    
    mov rbx, r11
    cmp ah, bh						; test if the page has changed.
    jne page_change

    add r14, 6						; Clock

    jmp opcode_done	

page_change:						; page change as a 1 cycle penalty
    add r14, 7						; Clock
    jmp opcode_done

endm

bbr_body macro bitnumber
    read_zp_rbx
    movzx rax, byte ptr[rsi+rbx]
    bt ax, bitnumber
    jnc branch
    add r11w, 2						; move PC on
    add r14, 5						; Clock

    jmp opcode_done	
branch:
    bb_perform_jump
endm

x0F_bbr0 proc
    bbr_body 0
x0F_bbr0 endp

x1F_bbr1 proc
    bbr_body 1
x1F_bbr1 endp

x2F_bbr2 proc
    bbr_body 2
x2F_bbr2 endp

x3F_bbr3 proc
    bbr_body 3
x3F_bbr3 endp

x4F_bbr4 proc
    bbr_body 4
x4F_bbr4 endp

x5F_bbr5 proc
    bbr_body 5
x5F_bbr5 endp

x6F_bbr6 proc
    bbr_body 6
x6F_bbr6 endp

x7F_bbr7 proc
    bbr_body 7
x7F_bbr7 endp

;
; BBS
;

bbs_body macro bitnumber
    read_zp_rbx
    movzx rax, byte ptr[rsi+rbx]
    bt ax, bitnumber
    jc branch
    add r11w, 2						; move PC on
    add r14, 5						; Clock

    jmp opcode_done	
branch:
    bb_perform_jump
endm

x8F_bbs0 proc
    bbs_body 0
x8F_bbs0 endp

x9F_bbs1 proc
    bbs_body 1
x9F_bbs1 endp

xAF_bbs2 proc
    bbs_body 2
xAF_bbs2 endp

xBF_bbs3 proc
    bbs_body 3
xBF_bbs3 endp

xCF_bbs4 proc
    bbs_body 4
xCF_bbs4 endp

xDF_bbs5 proc
    bbs_body 5
xDF_bbs5 endp

xEF_bbs6 proc
    bbs_body 6
xEF_bbs6 endp

xFF_bbs7 proc
    bbs_body 7
xFF_bbs7 endp

;
; JMP
;

x4C_jmp_abs proc
    read_abs_rbx 0
    mov r11w, bx	

    add r14, 3
    jmp opcode_done

x4C_jmp_abs endp

x6C_jmp_ind proc
    read_abs_rbx 0	; get address to bx

    mov r11w, word ptr [rsi+rbx] ; Set to PC

    add r14, 5
    jmp opcode_done
x6C_jmp_ind endp

x7C_jmp_indx proc
    read_abs_rbx 0	; get address to bx
    add bx, r9w		; Add x

    mov r11w, word ptr [rsi+rbx] ; Set to PC

    add r14, 6
    jmp opcode_done
x7C_jmp_indx endp

;
; Subroutines
;

x20_jsr proc
    movzx rbx, word ptr [rdx].state.stackpointer			; Get stack pointer

    ; store stack info for debugging
    mov rcx, r11
    sub rcx, 1
    mov ax, word ptr [rsi] ; ram + rom
    shl rax, 16
    or rcx, rax

    mov rdi, qword ptr [rdx].state.stackinfo_ptr

    mov rax, r11						; Get PC + 1 as the return address (to put address-1 on the stack)
    add rax, 1

    mov dword ptr [rdi + rbx * 4 - 400h], ecx

    mov [rsi + rbx], ah					; Put PC Low byte on stack
    sub bl, 1							; Move stack pointer on

    ; store stack info for debuggin
    mov dword ptr [rdi + rbx * 4 - 400h], ecx

    mov [rsi + rbx], al					; Put PC High byte on stack
    sub bl, 1							; Move stack pointer on (done twice for wrapping)

    mov byte ptr [rdx].state.stackpointer, bl	; Store stack pointer


    read_abs_rbx 0						; use macro to get destination
    mov r11w, bx	

    add r14, 6							; Add cycles

    jmp opcode_done
x20_jsr endp

x60_rts proc
    movzx rbx, word ptr [rdx].state.stackpointer			; Get stack pointer

    add bl, 1							; Move stack pointer on
    mov al, [rsi+rbx]					; Get PC High byte on stack
    add bl, 1							; Move stack pointer on (done twice for wrapping)
    mov ah, [rsi+rbx]					; Get PC Low byte on stack

    mov byte ptr [rdx].state.stackpointer, bl	; Store stack pointer

    add ax, 1							; Add on 1 for the next byte
    mov r11w, ax						; Set PC to destination

    add r14, 6							; Add cycles

    ; check for stack breakpoint
    mov rdi, qword ptr [rdx].state.stackbreakpoint_ptr
    and rbx, 0ffh
    movzx rax, byte ptr [rdi + rbx]
    mov byte ptr [rdi + rbx], 0
    mov dword ptr [rdx].state.stack_breakpoint_hit, eax

    jmp opcode_done
x60_rts endp

;
; Stack
;

x48_pha proc
    movzx rbx, word ptr [rdx].state.stackpointer			; Get stack pointer
    sub byte ptr [rdx].state.stackpointer, 1	; Decrement stack pointer
    mov [rsi + rbx], r8b					; Put A on stack

    ; store stack info for debugging
    mov rcx, r11
    sub rcx, 1
    mov ax, word ptr [rsi] ; ram
    shl rax, 16
    or rcx, rax
    mov rdi, qword ptr [rdx].state.stackinfo_ptr
    mov dword ptr [rdi + rbx * 4 - 400h], ecx
    
    add r14, 3							; Add cycles

    jmp opcode_done

x48_pha endp

x68_pla proc
    add byte ptr [rdx].state.stackpointer, 1	; Increment stack pointer
    movzx rbx, word ptr [rdx].state.stackpointer			; Get stack pointer

    mov r8b, byte ptr [rsi+rbx] 		; Pull A from the stack
    test r8b, r8b
    write_flags_r15_preservecarry
    
    add r14, 4							; Add cycles

    jmp opcode_done
x68_pla endp

xDA_phx proc
    movzx rbx, word ptr [rdx].state.stackpointer			; Get stack pointer

    mov [rsi+rbx], r9b					; Put X on stack
    dec byte ptr [rdx].state.stackpointer		; Decrement stack pointer
    
    ; store stack info for debugging
    mov rcx, r11
    sub rcx, 1
    mov ax, word ptr [rsi] ; ram
    shl rax, 16
    or rcx, rax
    mov rdi, qword ptr [rdx].state.stackinfo_ptr
    mov dword ptr [rdi + rbx * 4 - 400h], ecx

    add r14, 3							; Add cycles

    jmp opcode_done

xDA_phx endp

xFA_plx proc
    add byte ptr [rdx].state.stackpointer, 1	; Increment stack pointer
    movzx rbx, word ptr [rdx].state.stackpointer			; Get stack pointer

    mov r9b, byte ptr [rsi+rbx] 		; Pull X from the stack
    test r9b, r9b
    write_flags_r15_preservecarry
    
    add r14, 4							; Add cycles

    jmp opcode_done
xFA_plx endp

x5A_phy proc	
    movzx rbx, word ptr [rdx].state.stackpointer			; Get stack pointer

    mov [rsi+rbx], r10b					; Put Y on stack
    dec byte ptr [rdx].state.stackpointer		; Decrement stack pointer

    ; store stack info for debugging
    mov rcx, r11
    sub rcx, 1
    mov ax, word ptr [rsi] ; ram
    shl rax, 16
    or rcx, rax
    mov rdi, qword ptr [rdx].state.stackinfo_ptr
    mov dword ptr [rdi + rbx * 4 - 400h], ecx
    
    add r14, 3							; Add cycles

    jmp opcode_done
x5A_phy endp

x7A_ply proc
    add byte ptr [rdx].state.stackpointer ,1	; Increment stack pointer
    movzx rbx, word ptr [rdx].state.stackpointer			; Get stack pointer

    mov rsi, [rdx].state.memory_ptr
    mov r10b, byte ptr [rsi+rbx] 		; Pull Y from the stack
    test r10b, r10b
    write_flags_r15_preservecarry
    
    add r14, 4							; Add cycles
    jmp opcode_done
x7A_ply endp

x9A_txs proc
    mov byte ptr [rdx].state.stackpointer, r9b ; move X to stack pointer
    add r14, 2							; Add cycles
    jmp opcode_done
x9A_txs endp

xBA_tsx proc
    mov r9b, byte ptr [rdx].state.stackpointer ; move stack pointer to X

    test r9b, r9b
    write_flags_r15_preservecarry

    add r14, 2							; Add cycles
    jmp opcode_done	
xBA_tsx endp

;
; Interrupt
;

; also used by PHP
set_status_register_al macro
    mov	al, 00100000b ; bits that are always set

    ; carry
    bt r15w, 0 +8
    jnc no_carry
    or al, 00000001b
no_carry:
    
    ; zero
    bt r15w, 6 +8
    jnc no_zero
    or al, 00000010b
no_zero:

    ; negative
    bt r15w, 7 +8
    jnc no_negative
    or al, 10000000b
no_negative:

    ; interrupt disable
    movzx rbx, byte ptr [rdx].state.flags_interruptDisable
    test bl, 1
    jz no_interrupt
    or al, 00000100b
no_interrupt:

    ; overflow
    movzx rbx, byte ptr [rdx].state.flags_overflow
    test bl, 1
    jz no_overflow
    or al, 01000000b
no_overflow:

    ; decimal
    movzx rbx, byte ptr [rdx].state.flags_decimal
    test bl, 1
    jz no_decimal
    or al, 00001000b
no_decimal:

endm

get_status_register macro preservebx
    movzx rbx, word ptr [rdx].state.stackpointer	; Get stack pointer
    add bl, 1							; Decrement stack pointer
    movzx rax, byte ptr [rsi+rbx]			; Get status from stack
    
    xor r15w, r15w

if preservebx eq 1
    push rbx
else
    mov byte ptr [rdx].state.stackpointer, bl
endif

    ; carry
    bt ax, 0
    jnc no_carry
    ;                |
    or r15w, 0000000100000000b
no_carry:

    ; zero
    bt ax, 1
    jnc no_zero
    ;                |
    or r15w, 0100000000000000b
no_zero:

    ; negative
    bt ax, 7
    jnc no_negative
    ;                |
    or r15w, 1000000000000000b
no_negative:

    ; interrupt disable
    bt ax, 2
    setc bl
    mov byte ptr [rdx].state.flags_interruptDisable, bl

    ; overflow
    bt ax, 6
    setc bl
    mov byte ptr [rdx].state.flags_overflow, bl

    ; break
    ;bt ax, 4
    ;setc bl
    ;mov byte ptr [rdx].state.flags_break, bl

    ; decimal
    bt ax, 3
    setc bl
    mov byte ptr [rdx].state.flags_decimal, bl

if preservebx eq 1
    pop rbx
endif

endm

; important: rcx is if the cpu waiting, so dont clobber this register.
; this might not be true anymore....
handle_interrupt proc
    ;mov byte ptr [rdx].state.interrupt, 0
    movzx rax, byte ptr [rdx].state.flags_interruptDisable
    test rax, rax
    jnz interupt_disabled


    mov rax, r11						; Get PC as the return address (to put address on the stack -- different to JSR)

    movzx rbx, word ptr [rdx].state.stackpointer	; Get stack pointer
    mov rdi, qword ptr [rdx].state.stackinfo_ptr

    mov [rsi+rbx], ah					; Put PC High byte on stack
    mov dword ptr [rdi + rbx * 4 - 400h], 0ffffffffh
    dec bl								; Move stack pointer on (done twice for wrapping)

    mov [rsi+rbx], al					; Put PC Low byte on stack

    mov ax, word ptr [rsi]
    shl eax, 16
    or eax, 0ffffh
    mov dword ptr [rdi + rbx * 4 - 400h], eax   ; has the ram and rom banks.
    dec bl								; Move stack pointer on

    push bx
    set_status_register_al
    pop bx
    and al, 11101111b					; dont set B

    mov [rsi+rbx], al					; Put P on stack
    mov dword ptr [rdi + rbx * 4 - 400h], 0ffffffffh
    dec bl								; Move stack pointer on

    mov byte ptr [rdx].state.stackpointer, bl	; Store stack pointer
    mov byte ptr [rdx].state.flags_decimal, 0	; clear decimal flag
    mov byte ptr [rdx].state.flags_interruptDisable, 1 ; set interrupt Disable to true, gets reverted at the rti

    ;
    ; Copy rom bank 0 to ram
    ;
    call copy_rombank0_to_memory

    mov rdi, [rdx].state.rom_ptr
    mov r11w, word ptr [rdi + 03ffeh] ; get address at $fffe of rom 0.

    add r14, 7							; Clock 

    jmp next_opcode

interupt_disabled:
    test rcx, rcx						; check if we're waiting
    jz next_opcode
    
cpu_waiting:
    xor rcx, rcx						; clear waiting
    mov byte ptr [rdx].state.cpu_waiting, 0
    add r14, 1							; Clock 
    jmp next_opcode
handle_interrupt endp

handle_nmi proc
    mov byte ptr [rdx].state.cpu_waiting, 0	; always clear waiting, but still jump to the vector

    mov rax, r11						; Get PC as the return address (to put address on the stack -- different to JSR)

    movzx rbx, word ptr [rdx].state.stackpointer	; Get stack pointer
    mov rdi, qword ptr [rdx].state.stackinfo_ptr

    mov [rsi+rbx], ah					; Put PC High byte on stack
    mov dword ptr [rdi + rbx * 4 - 400h], 0fffffffeh
    dec bl								; Move stack pointer on (done twice for wrapping)
    mov [rsi+rbx], al					; Put PC Low byte on stack

    mov ax, word ptr [rsi]
    shl eax, 16
    or eax, 0ffffh
    mov dword ptr [rdi + rbx * 4 - 400h], eax   ; has the ram and rom banks.

    dec bl								; Move stack pointer on
    
    push bx
    set_status_register_al
    pop bx
    and al, 11101111b					; dont set B

    mov [rsi+rbx], al					; Put P on stack
    mov dword ptr [rdi + rbx * 4 - 400h], 0fffffffeh
    dec bl								; Move stack pointer on (done twice for wrapping)

    mov byte ptr [rdx].state.stackpointer, bl	; Store stack pointer
    mov byte ptr [rdx].state.flags_interruptDisable, 1 ; set interrupt Disable to true, gets reverted at the rti

    ;
    ; Copy rom bank 0 to ram
    ;
    call copy_rombank0_to_memory

    mov rdi, [rdx].state.rom_ptr
    mov r11w, word ptr [rdi + 03ffah] ; get address at $fffa of current rom

    add r14, 7							; Clock 

    jmp next_opcode

handle_nmi endp

x40_rti proc
    get_status_register	1				; set bx to stack pointer
    inc bl
    mov al, [rsi+rbx]					; low PC byte
    inc bl							
    mov ah, [rsi+rbx]					; high PC byte
    mov r11w, ax						; set PC
    mov byte ptr [rdx].state.stackpointer, bl	; Store stack pointer

    add r14, 6							; Clock 
    
    jmp opcode_done
x40_rti endp

;
; PHP\PLP
;

x08_php proc
    set_status_register_al

    movzx rbx, word ptr [rdx].state.stackpointer	; Get stack pointer
    sub byte ptr [rdx].state.stackpointer, 1		; Increment stack pointer
    mov [rsi+rbx], al								; Put status on stack

    ; store stack info for debugging
    mov rcx, r11
    sub rcx, 1
    mov ax, word ptr [rsi] ; ram
    shl rax, 16
    or rcx, rax
    mov rdi, qword ptr [rdx].state.stackinfo_ptr
    mov dword ptr [rdi + rbx * 4 - 400h], ecx
    
    add r14, 3										; Add cycles

    jmp opcode_done

x08_php endp

x28_plp proc
    get_status_register 0

    add r14, 4							; Add cycles

    jmp opcode_done
x28_plp endp

;
; BIT
;

bit_body_end macro checkvera, clock, pc
;    and dil, r8b				; cant just test, as we need to check bit 6 for overflow.
    test dil, dil				; sets zero and sign flags, we will overwrite zero later
    write_flags_r15_preservecarry
    
    bt di, 6				    ; test overflow
    setc byte ptr [rdx].state.flags_overflow

    ; check the zero flag, which is the and of input and the accumulator
    and dil, r8b
    test dil, dil
    lahf
    and rax, 0100000000000000b ; isolate zero
    and r15, 1011111111111111b ; remove zero
    or r15, rax                ; map on

    step_vera_read checkvera
    
    add r14, clock			
    add r11w, pc			

    jmp opcode_done
endm

bit_body_end_nochangetovn macro checkvera, clock, pc
    ; no call to write flags, as only Z can change.    
    ; check the zero flag, which is the and of input and the accumulator
    and dil, r8b
    test dil, dil
    lahf
    and rax, 0100000000000000b ; isolate zero
    and r15, 1011111111111111b ; remove zero
    or r15, rax                ; map on

    step_vera_read checkvera
    
    add r14, clock			
    add r11w, pc			

    jmp opcode_done
endm


bit_body macro checkvera, clock, pc
    movzx rdi, byte ptr [rsi+rbx]
    bit_body_end checkvera, clock, pc
endm

x89_bit_imm proc
    movzx rdi, byte ptr [rsi+r11]
    bit_body_end_nochangetovn 0, 3, 1
x89_bit_imm endp

x2C_bit_abs proc
    read_abs_rbx 0
    bit_body 1, 4, 2
x2C_bit_abs endp

x3C_bit_absx proc
    read_absx_rbx 0
    bit_body 1, 4, 2
x3C_bit_absx endp

x24_bit_zp proc
    read_zp_rbx
    bit_body 0, 3, 1
x24_bit_zp endp

x34_bit_zpx proc
    read_zpx_rbx
    bit_body 0, 3, 1
x34_bit_zpx endp

;
; TRB
;

x1C_trb_abs proc
    read_abs_rbx 0
    pre_write_check 1

    mov rax, r8
    not al
    and byte ptr [rsi+rbx], al
    jz set_zero
    
    step_io_readwrite 1
    add r14, 6
    add r11w, 2
    jmp opcode_done

skip:

    mov rax, r8
    not al
    and al, byte ptr [rsi+rbx]
    jz set_zero
    
    step_io_readwrite 1
    add r14, 6
    add r11w, 2
    jmp opcode_done
    
set_zero:
    ;        NZ A P C
    or r15w, 0100000000000000b
    step_io_readwrite 1
    

    add r14, 6
    add r11w, 2
    jmp opcode_done
x1C_trb_abs endp

x14_trb_zp proc
    read_zp_rbx

    mov rax, r8
    not al
    and byte ptr [rsi+rbx], al
    jz set_zero
    
    add r14, 5
    add r11w, 1			
    jmp opcode_done

set_zero:
    ;        NZ A P C
    or r15w, 0100000000000000b
    
    add r14, 5
    add r11w, 1			
    jmp opcode_done
x14_trb_zp endp

;
; TSB
;

x0C_tsb_abs proc
    read_abs_rbx 0
    pre_write_check 1

    or byte ptr [rsi+rbx], r8b
    jz set_zero
    
    step_io_readwrite 1
    add r14, 6
    add r11w, 2
    jmp opcode_done
    
skip:
    mov rax, r8
    or al, byte ptr [rsi+rbx]
    jz set_zero
    
    step_io_readwrite 1
    add r14, 5
    add r11w, 1
    jmp opcode_done

set_zero:
    ;        NZ A P C
    or r15w, 0100000000000000b
    step_io_readwrite 1

    add r14, 6
    add r11w, 2
    jmp opcode_done
x0C_tsb_abs endp

x04_tsb_zp proc
    read_zp_rbx

    or byte ptr [rsi+rbx], r8b

    jz set_zero
    
    add r14, 5
    add r11w, 1
    jmp opcode_done

set_zero:
    ;        NZ A P C
    or r15w, 0100000000000000b
    
    add r14, 5
    add r11w, 1
    jmp opcode_done
x04_tsb_zp endp

;
; RMB
;

rmb_body macro mask
    read_zp_rbx

    and byte ptr [rsi+rbx], mask
    
    add r14, 5
    add r11w, 1
    jmp opcode_done
endm

x07_rmb0 proc
    rmb_body 11111110b
x07_rmb0 endp

x17_rmb1 proc
    rmb_body 11111101b
x17_rmb1 endp

x27_rmb2 proc
    rmb_body 11111011b
x27_rmb2 endp

x37_rmb3 proc
    rmb_body 11110111b
x37_rmb3 endp

x47_rmb4 proc
    rmb_body 11101111b
x47_rmb4 endp

x57_rmb5 proc
    rmb_body 11011111b
x57_rmb5 endp

x67_rmb6 proc
    rmb_body 10111111b
x67_rmb6 endp

x77_rmb7 proc
    rmb_body 01111111b	
x77_rmb7 endp

;
; SMB
;

smb_body macro mask
    read_zp_rbx

    or byte ptr [rsi+rbx], mask
    
    add r14, 5
    add r11w, 1
    jmp opcode_done
endm

x87_smb0 proc
    smb_body 00000001b
x87_smb0 endp

x97_smb1 proc
    smb_body 00000010b
x97_smb1 endp

xa7_smb2 proc
    smb_body 00000100b
xa7_smb2 endp

xb7_smb3 proc
    smb_body 00001000b
xb7_smb3 endp

xc7_smb4 proc
    smb_body 00010000b
xc7_smb4 endp

xd7_smb5 proc
    smb_body 00100000b
xd7_smb5 endp

xe7_smb6 proc
    smb_body 01000000b
xe7_smb6 endp

xf7_smb7 proc
    smb_body 10000000b
xf7_smb7 endp

;
; NOP
;

xEA_nop proc
    add r14, 2	; Clock	
    jmp opcode_done
xEA_nop endp

;
; Wait
;

xCB_wai proc
    add r14, 2	; Clock	
    mov [rdx].state.cpu_waiting, 1
    jmp opcode_done	
xCB_wai endp

;
; BRK - NOT YET TESTED
;

x00_brk proc
    ; store stack info for debugging
    mov rcx, r11
    sub rcx, 1
    mov ax, word ptr [rsi] ; ram
    shl rax, 16
    or rcx, rax
    mov rdi, qword ptr [rdx].state.stackinfo_ptr

    mov rax, r11						; Get PC as the return address (to put address on the stack -- different to JSR)

    movzx rbx, word ptr [rdx].state.stackpointer	; Get stack pointer
    mov [rsi+rbx], ah					; Put PC High byte on stack
    mov dword ptr [rdi + rbx * 4 - 400h], ecx
    dec bl								; Move stack pointer on (done twice for wrapping)
    mov [rsi+rbx], al					; Put PC Low byte on stack
    mov dword ptr [rdi + rbx * 4 - 400h], ecx
    dec bl								; Move stack pointer on

    push bx
    set_status_register_al
    or al, 00010000b                    ; set break flag
    pop bx								; no need to change as brk is set here.

    mov [rsi+rbx], al					; Put P on stack
    mov dword ptr [rdi + rbx * 4 - 400h], ecx
    dec bl								; Move stack pointer on (done twice for wrapping)

    mov byte ptr [rdx].state.flags_decimal, 0		; disable decimal flag
    mov byte ptr [rdx].state.stackpointer, bl		; Store stack pointer
    mov byte ptr [rdx].state.flags_interruptDisable, 1 ; set interrupt Disable to true, gets reverted at the rti

    ;
    ; Copy rom bank 0 to ram
    ;
    call copy_rombank0_to_memory

    mov rdi, [rdx].state.rom_ptr
    mov r11w, word ptr [rdi + 03ffeh] ; get address at $fffe of current rom

    add r14, 7							; Clock 

    mov eax, dword ptr [rdx].state.brk_causes_stop
    test eax, eax
    jz next_opcode

stop_emulation:
    write_state_obj
    
    push rdx
    ; dont need to save CPU registers, as they are now stored
    call qword ptr [rdx].state.get_ticks    ; ticks to rax
    pop rdx

    mov [rdx].state.clock_pause, rax

    restore_registers

    mov rax, 03h

    leave
    ret

x00_brk endp

;
; Exit
;

xDB_stp proc

    add r14, 3	; Clock
    
    call vera_render_display

    ; return stp was hit.
    write_state_obj

    push rdx
    ; dont need to save CPU registers, as they are now stored
    call qword ptr [rdx].state.get_ticks    ; ticks to rax
    pop rdx

    mov [rdx].state.clock_pause, rax

    restore_registers

    mov rax, 02h

    leave
    ret

xDB_stp endp

noinstruction PROC

    ; return error	
    write_state_obj

    push rdx
    ; dont need to save CPU registers, as they are now stored
    call qword ptr [rdx].state.get_ticks    ; ticks to rax
    pop rdx

    mov [rdx].state.clock_pause, rax

    restore_registers

    mov rax, 01h

    leave
    ret
    
noinstruction ENDP

noinstruction_nop_1_1 proc
    add r14, 1	; Clock	
    jmp opcode_done
    ret
noinstruction_nop_1_1 endp

noinstruction_nop_2_1 proc
    add r14, 2	; Clock	
    jmp opcode_done
    ret
noinstruction_nop_2_1 endp

noinstruction_nop_4_1 proc
    add r14, 4	; Clock	
    jmp opcode_done
    ret
noinstruction_nop_4_1 endp

noinstruction_nop_8_1 proc
    add r14, 8	; Clock	
    jmp opcode_done
    ret
noinstruction_nop_8_1 endp


noinstruction_nop_2_2 proc
    add r14, 2	; Clock	
    inc r11
    jmp opcode_done
    ret
noinstruction_nop_2_2 endp

noinstruction_nop_3_2 proc
    add r14, 3	; Clock	
    inc r11
    jmp opcode_done
    ret
noinstruction_nop_3_2 endp

noinstruction_nop_4_2 proc
    add r14, 4	; Clock	
    inc r11
    jmp opcode_done
    ret
noinstruction_nop_4_2 endp

;
; Opcode jump table
;
; all opcodes, in order of value starting with 0x00
; should have 76 free when done!
align 8
instructions_table:
opcode_00	qword	x00_brk 		- instructions_table ; $00
opcode_01	qword	x01_ora_indx 	- instructions_table ; $01
opcode_02	qword	noinstruction_nop_2_2 	- instructions_table ; $02
opcode_03	qword	noinstruction_nop_1_1 	- instructions_table ; $03
opcode_04	qword	x04_tsb_zp	 	- instructions_table ; $04
opcode_05	qword	x05_ora_zp	 	- instructions_table ; $05
opcode_06	qword	x06_asl_zp	 	- instructions_table ; $06
opcode_07	qword	x07_rmb0	 	- instructions_table ; $07
opcode_08	qword	x08_php		 	- instructions_table ; $08
opcode_09	qword	x09_ora_imm	 	- instructions_table ; $09
opcode_0A	qword	x0A_asl_a	 	- instructions_table ; $0A
opcode_0B	qword	noinstruction_nop_1_1 	- instructions_table ; $0B
opcode_0C	qword	x0C_tsb_abs 	- instructions_table ; $0C
opcode_0D	qword	x0D_ora_abs	 	- instructions_table ; $0D
opcode_0E	qword	x0E_asl_abs	 	- instructions_table ; $0E
opcode_0F	qword	x0F_bbr0	 	- instructions_table ; $0F
opcode_10	qword	x10_bpl		 	- instructions_table ; $10
opcode_11	qword	x11_ora_indy 	- instructions_table ; $11
opcode_12	qword	x12_ora_indzp 	- instructions_table ; $12
opcode_13	qword	noinstruction_nop_1_1 	- instructions_table ; $13
opcode_14	qword	x14_trb_zp	 	- instructions_table ; $14
opcode_15	qword	x15_ora_zpx	 	- instructions_table ; $15
opcode_16	qword	x16_asl_zpx	 	- instructions_table ; $16
opcode_17	qword	x17_rmb1	 	- instructions_table ; $17
opcode_18	qword	x18_clc		 	- instructions_table ; $18
opcode_19	qword	x19_ora_absy 	- instructions_table ; $19
opcode_1A	qword	x1A_inc_a	 	- instructions_table ; $1A
opcode_1B	qword	noinstruction_nop_1_1 	- instructions_table ; $1B
opcode_1C	qword	x1C_trb_abs	 	- instructions_table ; $1C
opcode_1D	qword	x1D_ora_absx 	- instructions_table ; $1D
opcode_1E	qword	x1E_asl_absx 	- instructions_table ; $1E
opcode_1F	qword	x1F_bbr1	 	- instructions_table ; $1F
opcode_20	qword	x20_jsr		 	- instructions_table ; $20
opcode_21	qword	x21_and_indx 	- instructions_table ; $21
opcode_22	qword	noinstruction_nop_2_2 	- instructions_table ; $22
opcode_23	qword	noinstruction_nop_1_1 	- instructions_table ; $23
opcode_24	qword	x24_bit_zp	 	- instructions_table ; $24
opcode_25	qword	x25_and_zp	 	- instructions_table ; $25
opcode_26	qword	x26_rol_zp	 	- instructions_table ; $26
opcode_27	qword	x27_rmb2	 	- instructions_table ; $27
opcode_28	qword	x28_plp		 	- instructions_table ; $28
opcode_29	qword	x29_and_imm 	- instructions_table ; $29
opcode_2A	qword	x2A_rol_a	 	- instructions_table ; $2A
opcode_2B	qword	noinstruction_nop_1_1 	- instructions_table ; $2B
opcode_2C	qword	x2C_bit_abs 	- instructions_table ; $2C
opcode_2D	qword	x2D_and_abs 	- instructions_table ; $2D
opcode_2E	qword	x2E_rol_abs 	- instructions_table ; $2E
opcode_2F	qword	x2F_bbr2	 	- instructions_table ; $2F
opcode_30	qword	x30_bmi		 	- instructions_table ; $30
opcode_31	qword	x31_and_indy 	- instructions_table ; $31
opcode_32	qword	x32_and_indzp 	- instructions_table ; $32
opcode_33	qword	noinstruction_nop_1_1 	- instructions_table ; $33
opcode_34	qword	x34_bit_zpx 	- instructions_table ; $34
opcode_35	qword	x35_and_zpx 	- instructions_table ; $35
opcode_36	qword	x36_rol_zpx 	- instructions_table ; $36
opcode_37	qword	x37_rmb3 		- instructions_table ; $37
opcode_38	qword	x38_sec		 	- instructions_table ; $38
opcode_39	qword	x39_and_absy 	- instructions_table ; $39
opcode_3A	qword	x3A_dec_a	 	- instructions_table ; $3A
opcode_3B	qword	noinstruction_nop_1_1 	- instructions_table ; $3B
opcode_3C	qword	x3C_bit_absx 	- instructions_table ; $3C
opcode_3D	qword	x3D_and_absx 	- instructions_table ; $3D
opcode_3E	qword	x3E_rol_absx 	- instructions_table ; $3E
opcode_3F	qword	x3F_bbr3	 	- instructions_table ; $3F
opcode_40	qword	x40_rti		 	- instructions_table ; $40
opcode_41	qword	x41_eor_indx 	- instructions_table ; $41
opcode_42	qword	noinstruction_nop_2_2 	- instructions_table ; $42
opcode_43	qword	noinstruction_nop_1_1 	- instructions_table ; $43
opcode_44	qword	noinstruction_nop_3_2 	- instructions_table ; $44
opcode_45	qword	x45_eor_zp	 	- instructions_table ; $45
opcode_46	qword	x46_lsr_zp	 	- instructions_table ; $46
opcode_47	qword	x47_rmb4	 	- instructions_table ; $47
opcode_48	qword	x48_pha		 	- instructions_table ; $48
opcode_49	qword	x49_eor_imm 	- instructions_table ; $49
opcode_4A	qword	x4A_lsr_a	 	- instructions_table ; $4A
opcode_4B	qword	noinstruction_nop_1_1 	- instructions_table ; $4B
opcode_4C	qword	x4C_jmp_abs 	- instructions_table ; $4C
opcode_4D	qword	x4D_eor_abs 	- instructions_table ; $4D
opcode_4E	qword	x4E_lsr_abs 	- instructions_table ; $4E
opcode_4F	qword	x4F_bbr4	 	- instructions_table ; $4F
opcode_50	qword	x50_bvc		 	- instructions_table ; $50
opcode_51	qword	x51_eor_indy 	- instructions_table ; $51
opcode_52	qword	x52_eor_indzp 	- instructions_table ; $52
opcode_53	qword	noinstruction_nop_1_1 	- instructions_table ; $53
opcode_54	qword	noinstruction_nop_4_2 	- instructions_table ; $54
opcode_55	qword	x55_eor_zpx 	- instructions_table ; $55
opcode_56	qword	x56_lsr_zpx 	- instructions_table ; $56
opcode_57	qword	x57_rmb5	 	- instructions_table ; $57
opcode_58	qword	x58_cli		 	- instructions_table ; $58
opcode_59	qword	x59_eor_absy 	- instructions_table ; $59
opcode_5A	qword	x5A_phy		 	- instructions_table ; $5A
opcode_5B	qword	noinstruction_nop_1_1 	- instructions_table ; $5B
opcode_5C	qword	noinstruction_nop_8_1 	- instructions_table ; $5C
opcode_5D	qword	x5D_eor_absx 	- instructions_table ; $5D
opcode_5E	qword	x5E_lsr_absx 	- instructions_table ; $5E
opcode_5F	qword	x5F_bbr5	 	- instructions_table ; $5F
opcode_60	qword	x60_rts		 	- instructions_table ; $60
opcode_61	qword	x61_adc_indx 	- instructions_table ; $61
opcode_62	qword	noinstruction_nop_2_2 	- instructions_table ; $62
opcode_63	qword	noinstruction_nop_1_1 	- instructions_table ; $63
opcode_64	qword	x64_stz_zp	 	- instructions_table ; $64
opcode_65	qword	x65_adc_zp	 	- instructions_table ; $65
opcode_66	qword	x66_ror_zp	 	- instructions_table ; $66
opcode_67	qword	x67_rmb6	 	- instructions_table ; $67
opcode_68	qword	x68_pla		 	- instructions_table ; $68
opcode_69	qword	x69_adc_imm 	- instructions_table ; $69
opcode_6A	qword	x6A_ror_a	 	- instructions_table ; $6A
opcode_6B	qword	noinstruction_nop_1_1 	- instructions_table ; $6B
opcode_6C	qword	x6C_jmp_ind 	- instructions_table ; $6C
opcode_6D	qword	x6D_adc_abs 	- instructions_table ; $6D
opcode_6E	qword	x6E_ror_abs 	- instructions_table ; $6E
opcode_6F	qword	x6F_bbr6	 	- instructions_table ; $6F
opcode_70	qword	x70_bvs		 	- instructions_table ; $70
opcode_71	qword	x71_adc_indy 	- instructions_table ; $71
opcode_72	qword	x72_adc_indzp 	- instructions_table ; $72
opcode_73	qword	noinstruction_nop_1_1 	- instructions_table ; $73
opcode_74	qword	x74_stz_zpx 	- instructions_table ; $74
opcode_75	qword	x75_adc_zpx 	- instructions_table ; $75
opcode_76	qword	x76_ror_zpx 	- instructions_table ; $76
opcode_77	qword	x77_rmb7	 	- instructions_table ; $77
opcode_78	qword	x78_sei		 	- instructions_table ; $78
opcode_79	qword	x79_adc_absy 	- instructions_table ; $79
opcode_7A	qword	x7A_ply		 	- instructions_table ; $7A
opcode_7B	qword	noinstruction_nop_1_1 	- instructions_table ; $7B
opcode_7C	qword	x7C_jmp_indx 	- instructions_table ; $7C
opcode_7D	qword	x7D_adc_absx 	- instructions_table ; $7D
opcode_7E	qword	x7E_ror_absx 	- instructions_table ; $7E
opcode_7F	qword	x7F_bbr7	 	- instructions_table ; $7F
opcode_80	qword	x80_bra		 	- instructions_table ; $80
opcode_81	qword	x81_sta_indx 	- instructions_table ; $81
opcode_82	qword	noinstruction_nop_2_2 	- instructions_table ; $82
opcode_83	qword	noinstruction_nop_1_1 	- instructions_table ; $83
opcode_84	qword	x84_sty_zp	 	- instructions_table ; $84
opcode_85	qword	x85_sta_zp	 	- instructions_table ; $85
opcode_86	qword	x86_stx_zp	 	- instructions_table ; $86
opcode_87	qword	x87_smb0	 	- instructions_table ; $87
opcode_88	qword	x88_dey		 	- instructions_table ; $88
opcode_89	qword	x89_bit_imm 	- instructions_table ; $89
opcode_8A	qword	x8A_txa		 	- instructions_table ; $8A
opcode_8B	qword	noinstruction_nop_1_1 	- instructions_table ; $8B
opcode_8C	qword	x8C_sty_abs 	- instructions_table ; $8C
opcode_8D	qword	x8D_sta_abs 	- instructions_table ; $8D
opcode_8E	qword	x8E_stx_abs 	- instructions_table ; $8E
opcode_8F	qword	x8F_bbs0	 	- instructions_table ; $8F
opcode_90	qword	x90_bcc		 	- instructions_table ; $90
opcode_91	qword	x91_sta_indy 	- instructions_table ; $91
opcode_92	qword	x92_sta_indzp 	- instructions_table ; $92
opcode_93	qword	noinstruction_nop_1_1 	- instructions_table ; $93
opcode_94	qword	x94_sty_zpx 	- instructions_table ; $94
opcode_95	qword	x95_sta_zpx 	- instructions_table ; $95
opcode_96	qword	x96_stx_zpy 	- instructions_table ; $96
opcode_97	qword	x97_smb1	 	- instructions_table ; $97
opcode_98	qword	x98_tya		 	- instructions_table ; $98
opcode_99	qword	x99_sta_absy 	- instructions_table ; $99
opcode_9A	qword	x9A_txs		 	- instructions_table ; $9A
opcode_9B	qword	noinstruction_nop_1_1 	- instructions_table ; $9B
opcode_9C	qword	x9C_stz_abs 	- instructions_table ; $9C
opcode_9D	qword	x9D_sta_absx 	- instructions_table ; $9D
opcode_9E	qword	x9E_stz_absx 	- instructions_table ; $9E
opcode_9F	qword	x9F_bbs1	 	- instructions_table ; $9F
opcode_A0	qword	xA0_ldy_imm 	- instructions_table ; $A0
opcode_A1	qword	xA1_lda_indx 	- instructions_table ; $A1
opcode_A2	qword	xA2_ldx_imm 	- instructions_table ; $A2
opcode_A3	qword	noinstruction_nop_1_1 	- instructions_table ; $A3
opcode_A4	qword	xA4_ldy_zp	 	- instructions_table ; $A4
opcode_A5	qword	xA5_lda_zp	 	- instructions_table ; $A5
opcode_A6	qword	xA6_ldx_zp	 	- instructions_table ; $A6
opcode_A7	qword	xA7_smb2	 	- instructions_table ; $A7
opcode_A8	qword	xA8_tay		 	- instructions_table ; $A8
opcode_A9	qword	xA9_lda_imm 	- instructions_table ; $A9
opcode_AA	qword	xAA_tax		 	- instructions_table ; $AA
opcode_AB	qword	noinstruction_nop_1_1 	- instructions_table ; $AB
opcode_AC	qword	xAC_ldy_abs 	- instructions_table ; $AC
opcode_AD	qword	xAD_lda_abs 	- instructions_table ; $AD
opcode_AE	qword	xAE_ldx_abs 	- instructions_table ; $AE
opcode_AF	qword	xAF_bbs2	 	- instructions_table ; $AF
opcode_B0	qword	xB0_bcs		 	- instructions_table ; $B0
opcode_B1	qword	xB1_lda_indy 	- instructions_table ; $B1
opcode_B2	qword	xB2_lda_indzp 	- instructions_table ; $B2
opcode_B3	qword	noinstruction_nop_1_1 	- instructions_table ; $B3
opcode_B4	qword	xB4_ldy_zpx 	- instructions_table ; $B4
opcode_B5	qword	xB5_lda_zpx 	- instructions_table ; $B5
opcode_B6	qword	xB6_ldx_zpy 	- instructions_table ; $B6
opcode_B7	qword	xB7_smb3	 	- instructions_table ; $B7
opcode_B8	qword	xB8_clv		 	- instructions_table ; $B8
opcode_B9	qword	xB9_lda_absy 	- instructions_table ; $B9
opcode_BA	qword	xBA_tsx		 	- instructions_table ; $BA
opcode_BB	qword	noinstruction_nop_1_1 	- instructions_table ; $BB
opcode_BC	qword	xBC_ldy_absx 	- instructions_table ; $BC
opcode_BD	qword	xBD_lda_absx 	- instructions_table ; $BD
opcode_BE	qword	xBE_ldx_absy 	- instructions_table ; $BE
opcode_BF	qword	xBF_bbs3 		- instructions_table ; $BF
opcode_C0	qword	xC0_cmpy_imm 	- instructions_table ; $C0
opcode_C1	qword	xC1_sbc_indx 	- instructions_table ; $C1
opcode_C2	qword	noinstruction_nop_2_2 	- instructions_table ; $C2
opcode_C3	qword	noinstruction_nop_1_1 	- instructions_table ; $C3
opcode_C4	qword	xC4_cmpy_zp 	- instructions_table ; $C4
opcode_C5	qword	xC5_cmp_zp	 	- instructions_table ; $C5
opcode_C6	qword	xC6_dec_zp	 	- instructions_table ; $C6
opcode_C7	qword	xC7_smb4	 	- instructions_table ; $C7
opcode_C8	qword	xC8_iny			- instructions_table ; $C8
opcode_C9	qword	xC9_cmp_imm 	- instructions_table ; $C9
opcode_CA	qword	xCA_dex		 	- instructions_table ; $CA
opcode_CB	qword	xCB_wai		 	- instructions_table ; $CB
opcode_CC	qword	xCC_cmpy_abs 	- instructions_table ; $CC
opcode_CD	qword	xCD_cmp_abs 	- instructions_table ; $CD
opcode_CE	qword	xCE_dec_abs 	- instructions_table ; $CE
opcode_CF	qword	xCF_bbs4 	 	- instructions_table ; $CF
opcode_D0	qword	xD0_bne		 	- instructions_table ; $D0
opcode_D1	qword	xD1_cmp_indy 	- instructions_table ; $D1
opcode_D2	qword	xD2_cmp_indzp 	- instructions_table ; $D2
opcode_D3	qword	noinstruction_nop_1_1 	- instructions_table ; $D3
opcode_D4	qword	noinstruction_nop_4_2 	- instructions_table ; $D4
opcode_D5	qword	xD5_cmp_zpx 	- instructions_table ; $D5
opcode_D6	qword	xD6_dec_zpx 	- instructions_table ; $D6
opcode_D7	qword	xD7_smb5 		- instructions_table ; $D7
opcode_D8	qword	xD8_cld		 	- instructions_table ; $D8
opcode_D9	qword	xD9_cmp_absy 	- instructions_table ; $D9
opcode_DA	qword	xDA_phx		 	- instructions_table ; $DA
opcode_DB	qword	xDB_stp		 	- instructions_table ; $DB
opcode_DC	qword	noinstruction_nop_4_1 	- instructions_table ; $DC
opcode_DD	qword	xDD_cmp_absx 	- instructions_table ; $DD
opcode_DE	qword	xDE_dec_absx 	- instructions_table ; $DE
opcode_DF	qword	xDF_bbs5	 	- instructions_table ; $DF
opcode_E0	qword	xE0_cmpx_imm 	- instructions_table ; $E0
opcode_E1	qword	xE1_sbc_indx 	- instructions_table ; $E1
opcode_E2	qword	noinstruction_nop_2_2 	- instructions_table ; $E2
opcode_E3	qword	noinstruction_nop_1_1 	- instructions_table ; $E3
opcode_E4	qword	xE4_cmpx_zp 	- instructions_table ; $E4
opcode_E5	qword	xE5_sbc_zp	 	- instructions_table ; $E5
opcode_E6	qword	xE6_inc_zp	 	- instructions_table ; $E6
opcode_E7	qword	xe7_smb6	 	- instructions_table ; $E7
opcode_E8	qword	xE8_inx	 		- instructions_table ; $E8
opcode_E9	qword	xE9_sbc_imm 	- instructions_table ; $E9
opcode_EA	qword	xEA_nop		 	- instructions_table ; $EA
opcode_EB	qword	noinstruction_nop_1_1 	- instructions_table ; $EB
opcode_EC	qword	xEC_cmpx_abs 	- instructions_table ; $EC
opcode_ED	qword	xED_sbc_abs 	- instructions_table ; $ED
opcode_EE	qword	xEE_inc_abs 	- instructions_table ; $EE
opcode_EF	qword	xEF_bbs6 		- instructions_table ; $EF
opcode_F0	qword	xF0_beq		 	- instructions_table ; $F0
opcode_F1	qword	xF1_sbc_indy 	- instructions_table ; $F1
opcode_F2	qword	xF2_sbc_indzp 	- instructions_table ; $F2
opcode_F3	qword	noinstruction_nop_1_1 	- instructions_table ; $F3
opcode_F4	qword	noinstruction_nop_4_2 	- instructions_table ; $F4
opcode_F5	qword	xF5_sbc_zpx 	- instructions_table ; $F5
opcode_F6	qword	xF6_inc_zpx 	- instructions_table ; $F6
opcode_F7	qword	xf7_smb7	 	- instructions_table ; $F7
opcode_F8	qword	xF8_sed		 	- instructions_table ; $F8
opcode_F9	qword	xF9_sbc_absy 	- instructions_table ; $F9
opcode_FA	qword	xFA_plx		 	- instructions_table ; $FA
opcode_FB	qword	noinstruction_nop_1_1 	- instructions_table ; $FB
opcode_FC	qword	noinstruction_nop_4_1 	- instructions_table ; $FC
opcode_FD	qword	xFD_sbc_absx 	- instructions_table ; $FD
opcode_FE	qword	xFE_inc_absx 	- instructions_table ; $FE
opcode_FF	qword	xFF_bbs7	 	- instructions_table ; $FF

.code

END