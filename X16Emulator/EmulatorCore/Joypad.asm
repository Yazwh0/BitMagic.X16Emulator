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

NESLATCH equ 00000100b
NESCLOCK equ 00001000b
NESDATA0 equ 10000000b
NESDATA1 equ 01000000b
NESDATA2 equ 00100000b
NESDATA3 equ 00010000b
NESMASK  equ 00001100b
NESDATA  equ 11110000b

; called when there is any change to the VIA output
; rax has the value of V_ORA
joypad_handledata proc
	push rax

	and rax, NESMASK							; mask our bits
	mov ebx, [rdx].state.joypad_previous
	mov [rdx].state.joypad_previous, eax		; store for next time

	mov r13d, eax
	xor r13d, ebx								; get changes

	jz exit										; get out if neither has a change

	; if the latch has gone 1->0 then we should clock in data
	bt eax, 2		; latch is zero
	jc skip_latch_is_now_zero

	bt r13d, 2
	jc clock_in_data	; change was set, so clock in data.
	
skip_latch_is_now_zero:
	and eax, r13d								; mask, as we only care about 0->1

	bt eax, 2		; latch is high?
	jnc no_latch

	mov r13, [rdx].state.joypad_live			; latch the data
	mov qword ptr [rdx].state.joypad, r13	

	; if we're latching, set all data bits high, gets set properly on the first clock
	mov eax, NESDATA
	movzx r13, byte ptr [rdx].state.via_register_a_invalue
	and r13, 011b
	or r13b, al
	and byte ptr [rdx].state.via_register_a_invalue, r13b


	mov rax, r13
	; ---- update ORA/PRA
	movzx r13, byte ptr [rdx].state.via_register_a_outvalue
	movzx rdi, byte ptr [rsi+V_DDRA]
	and r13, rdi					; keep outbound values
	xor rdi, 0ffh
	and rax, rdi					; values in
	or r13, rax						; merge values inbound w/ outbound
	mov byte ptr [rsi+V_PRA], r13b	; store
	mov byte ptr [rsi+V_ORA], r13b

	pop rax
	ret

no_latch:
	bt eax, 3		; clock has gone high?
	jnc exit

clock_in_data:

	; clock in data and shift
	mov r13, qword ptr [rdx].state.joypad		; get all data
	mov rax, r13								; store

	; copy data bits to register a invalue
	movzx r12, byte ptr [rdx].state.via_register_a_invalue
	and r12, 00000011b							; keep i2c data!

	; take bottom bits from all 4 inputs and map
	xor ebx, ebx
	bt rax, 0
	setc bl
	shl ebx, 4
	or r12d, ebx
	
	xor ebx, ebx
	bt rax, 16
	setc bl
	shl ebx, 5
	or r12d, ebx

	xor ebx, ebx
	bt rax, 32
	setc bl
	shl ebx, 6
	or r12d, ebx

	xor ebx, ebx
	bt rax, 48
	setc bl
	shl ebx, 7
	or r12d, ebx

	mov byte ptr [rdx].state.via_register_a_invalue, r12b
	
	mov rbx, qword ptr bitmask

	shr r13, 1									 ; shift
	and r13, rbx								 ; mask off top bits
	or r13, qword ptr [rdx].state.joypad_newmask ; add on the new values - they differ if a joystick is present or not
	mov qword ptr [rdx].state.joypad, r13		 ; save for next time

	mov rax, r12
	; ---- update ORA/PRA
	movzx r13, byte ptr [rdx].state.via_register_a_outvalue
	movzx rdi, byte ptr [rsi+V_DDRA]
	and r13, rdi					; keep outbound values
	xor rdi, 0ffh
	and rax, rdi					; values in
	or r13, rax						; merge values inbound w/ outbound
	mov byte ptr [rsi+V_PRA], r13b	; store
	mov byte ptr [rsi+V_ORA], r13b

exit:
	pop rax
	ret

bitmask:
	word 07fffh
	word 07fffh	
	word 07fffh	
	word 07fffh	
joypad_handledata endp
