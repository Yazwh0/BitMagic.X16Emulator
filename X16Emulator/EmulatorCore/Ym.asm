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

include State.asm

; Cpu ticks per audio sample :
; Cpu Freq / (YM Freq / Samples)
; 8000000 / (3579545 / 64)
;
;
; So needs to be called every ~143.0349 CPU ticks
; rax = clock_ymnext
ym_render_audio proc
	add rax, 143

	mov ebx, [rdx].state.ym_cpu_partial
	add ebx, 349
	cmp ebx, 1000
	jl ymnext_done

	sub ebx, 1000
	inc rax

ymnext_done:
	mov [rdx].state.ym_cpu_partial, ebx
	mov [rdx].state.clock_ymnext, rax

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

	call [rdx].state.step_ym

	pop rcx
	pop rdx
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

	ret
ym_render_audio endp

ym_write_address proc
	movzx r13, byte ptr [rsi + rbx]
	mov [rdx].state.ym_address, r13d
	mov byte ptr [rsi + rbx], 0 ; todo: what actually does it return?

	ret
ym_write_address endp


ym_write_data proc
	movzx r13, byte ptr [rsi + rbx]
	mov [rdx].state.ym_data, r13d
	;mov byte ptr [rsi + rbx], 0 ; todo: status byte

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

	call [rdx].state.write_registers_ym ; will udpate the status byte

	pop rcx
	pop rdx
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

	ret	
ym_write_data endp
