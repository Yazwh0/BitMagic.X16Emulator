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

MAJOR_VERSION equ 43
MINOR_VERSION equ 0
PATCH_VERSION equ 0


; read the state of the i2c bus and set state as required
; expects 
; rbx :- message (alternativley in i2c_transmit)
smc_receive_data proc
	mov eax, dword ptr [rdx].state.smc_datacount
	cmp eax, 2
	jg overflow

	mov dword ptr [rdx].state.smc_offset, ebx
	lea rdi, [rdx].state.smc_data
	mov byte ptr [rdi + rax], bl
	inc eax
	mov dword ptr [rdx].state.smc_datacount, eax

overflow:
	ret
smc_receive_data endp

; called when a stp siginal is received, so can process the received data
smc_stop proc
	; todo, this should check read / write.

	lea rdi, [rdx].state.smc_data
	movzx rbx, byte ptr [rdi]						; get offset
	mov dword ptr [rdx].state.smc_offset, ebx		; store for write operations (eg keyb\mouse)

	cmp ebx, 7
	jg unknown_command

	lea rax, smc_commands
	add rax, [rax + rbx * 8]
	jmp rax

align 8
smc_commands:
	qword smc_donothing		- smc_commands; 0
	qword smc_power			- smc_commands; 1
	qword smc_reset			- smc_commands; 2
	qword smc_nmibutton		- smc_commands; 3
	qword smc_donothing		- smc_commands; 4
	qword smc_activityled	- smc_commands; 5
	qword smc_donothing		- smc_commands; 6
	qword smc_keyboard		- smc_commands; 7

unknown_command:
	mov dword ptr [rdx].state.smc_datacount, 0
	mov dword ptr [rdx].state.smc_data, 0
	ret
smc_stop endp

; return rbx as data to transmit
smc_set_next_write proc
	mov eax, dword ptr [rdx].state.smc_offset
	cmp eax, 03fh
	jg smc_sendnothing

	lea rdi, smc_transmit
	add rdi, [rdi + rax * 8]
	jmp rdi

align 8
smc_transmit:
	qword smc_sendnothing	- smc_transmit ; 00
	qword smc_sendnothing	- smc_transmit ; 01
	qword smc_sendnothing	- smc_transmit ; 02
	qword smc_sendnothing	- smc_transmit ; 03
	qword smc_sendnothing	- smc_transmit ; 04
	qword smc_sendnothing	- smc_transmit ; 05
	qword smc_sendnothing	- smc_transmit ; 06
	qword keyboard			- smc_transmit ; 07
	qword smc_sendnothing	- smc_transmit ; 08
	qword smc_sendnothing	- smc_transmit ; 09
	qword smc_sendnothing	- smc_transmit ; 0a
	qword smc_sendnothing	- smc_transmit ; 0b
	qword smc_sendnothing	- smc_transmit ; 0c
	qword smc_sendnothing	- smc_transmit ; 0d
	qword smc_sendnothing	- smc_transmit ; 0e
	qword smc_sendnothing	- smc_transmit ; 0f
	qword smc_sendnothing	- smc_transmit ; 10
	qword smc_sendnothing	- smc_transmit ; 11
	qword smc_sendnothing	- smc_transmit ; 12
	qword smc_sendnothing	- smc_transmit ; 13
	qword smc_sendnothing	- smc_transmit ; 14
	qword smc_sendnothing	- smc_transmit ; 15
	qword smc_sendnothing	- smc_transmit ; 16
	qword smc_sendnothing	- smc_transmit ; 17
	qword smc_sendnothing	- smc_transmit ; 18
	qword smc_sendnothing	- smc_transmit ; 19
	qword smc_sendnothing	- smc_transmit ; 1a
	qword smc_sendnothing	- smc_transmit ; 1b
	qword smc_sendnothing	- smc_transmit ; 1c
	qword smc_sendnothing	- smc_transmit ; 1d
	qword smc_sendnothing	- smc_transmit ; 1e
	qword smc_sendnothing	- smc_transmit ; 1f
	qword smc_sendnothing	- smc_transmit ; 20
	qword mouse				- smc_transmit ; 21
	qword mouse_device_id	- smc_transmit ; 22
	qword smc_sendnothing	- smc_transmit ; 23
	qword smc_sendnothing	- smc_transmit ; 24
	qword smc_sendnothing	- smc_transmit ; 25
	qword smc_sendnothing	- smc_transmit ; 26
	qword smc_sendnothing	- smc_transmit ; 27
	qword smc_sendnothing	- smc_transmit ; 28
	qword smc_sendnothing	- smc_transmit ; 29
	qword smc_sendnothing	- smc_transmit ; 2a
	qword smc_sendnothing	- smc_transmit ; 2b
	qword smc_sendnothing	- smc_transmit ; 2c
	qword smc_sendnothing	- smc_transmit ; 2d
	qword smc_sendnothing	- smc_transmit ; 2e
	qword smc_sendnothing	- smc_transmit ; 2f
	qword smc_major_version	- smc_transmit ; 30
	qword smc_minor_version	- smc_transmit ; 31
	qword smc_patch_version	- smc_transmit ; 32
	qword smc_sendnothing	- smc_transmit ; 33
	qword smc_sendnothing	- smc_transmit ; 34
	qword smc_sendnothing	- smc_transmit ; 35
	qword smc_sendnothing	- smc_transmit ; 36
	qword smc_sendnothing	- smc_transmit ; 37
	qword smc_sendnothing	- smc_transmit ; 38
	qword smc_sendnothing	- smc_transmit ; 39
	qword smc_sendnothing	- smc_transmit ; 3a
	qword smc_sendnothing	- smc_transmit ; 3b
	qword smc_sendnothing	- smc_transmit ; 3c
	qword smc_sendnothing	- smc_transmit ; 3d
	qword smc_sendnothing	- smc_transmit ; 3e
	qword smc_sendnothing	- smc_transmit ; 3f


smc_sendnothing:
	mov dword ptr [rdx].state.i2c_datatotransmit, 1
	mov ebx, 0ffh ; In this case the SMC wouldn't respond, so the CPU would just clock in 1s as thats the default.
	ret

mouse:
	mov eax, dword ptr [rdx].state.smc_mouse_readposition
	mov r13d, dword ptr [rdx].state.smc_mouse_writeposition

	cmp eax, r13d
	je no_mouse_data

	mov rbx, qword ptr [rdx].state.smc_mouse_ptr
	movzx rbx, byte ptr [rbx + rax]			; set ebx to the return value
	inc rax
	and rax, 8-1

	mov dword ptr [rdx].state.smc_mouse_readposition, eax

	mov dword ptr [rdx].state.i2c_datatotransmit, 1 ; not sure what this is for todo: check and remove if not used!!

	ret
no_mouse_data:
	xor rbx, rbx
	mov dword ptr [rdx].state.i2c_datatotransmit, 0
	ret


mouse_device_id:
	mov dword ptr [rdx].state.i2c_datatotransmit, 1
	mov rbx, 0
	ret

keyboard:
	mov eax, dword ptr [rdx].state.smc_keyboard_readposition
	mov r13d, dword ptr [rdx].state.smc_keyboard_writeposition

	cmp eax, r13d
	je no_keyboard_data

	mov rbx, qword ptr [rdx].state.smc_keyboard_ptr
	movzx rbx, byte ptr [rbx + rax]			; set ebx to the return value
	inc rax
	and rax, 16-1

	mov dword ptr [rdx].state.smc_keyboard_readposition, eax

	xor r12, r12
	cmp eax, r13d	; check if we're the same now, if so there is no more data
	sete r12b

	mov dword ptr [rdx].state.smc_keyboard_readnodata, r12d
	xor r12, 1
	mov dword ptr [rdx].state.i2c_datatotransmit, r12d

	ret
no_keyboard_data:
	xor rbx, rbx
	mov dword ptr [rdx].state.smc_keyboard_readnodata, 1
	mov dword ptr [rdx].state.i2c_datatotransmit, 0
	ret

smc_major_version:
	mov dword ptr [rdx].state.i2c_datatotransmit, 1
	mov rbx, MAJOR_VERSION
	ret
smc_minor_version:
	mov dword ptr [rdx].state.i2c_datatotransmit, 1
	mov rbx, MINOR_VERSION
	ret
smc_patch_version:
	mov dword ptr [rdx].state.i2c_datatotransmit, 1
	mov rbx, PATCH_VERSION
	ret
smc_set_next_write endp

; todo: remove
smc_complete_write proc
	ret
	mov eax, dword ptr [rdx].state.smc_keyboard_readposition
	inc rax
	and rax, 16-1
	mov dword ptr [rdx].state.smc_keyboard_readposition, eax
	ret
smc_complete_write endp

smc_power proc
	movzx rax, byte ptr [rdi+1]
	cmp rax, 1
	jg no_change
	je reset
	mov dword ptr [rdx].state.control, 2	; todo: use smc_powerdown return code
	mov dword ptr [rdx].state.exit_code, EXIT_SMC_POWEROFF
	mov dword ptr [rdx].state.smc_datacount, 0
	mov dword ptr [rdx].state.smc_data, 0
	ret

reset:
	mov dword ptr [rdx].state.control, 2	; todo: use smc_powerdown return code
	mov dword ptr [rdx].state.exit_code, EXIT_SMC_RESET

no_change:

	mov dword ptr [rdx].state.smc_datacount, 0
	mov dword ptr [rdx].state.smc_data, 0
	ret
smc_power endp

smc_reset proc
	movzx rax, byte ptr [rdi+1]
	test rax, rax
	jnz no_change
	mov dword ptr [rdx].state.control, 2	; todo: use smc_powerdown return code
	mov dword ptr [rdx].state.exit_code, EXIT_SMC_RESET
no_change:

	mov dword ptr [rdx].state.smc_datacount, 0
	mov dword ptr [rdx].state.smc_data, 0
	ret
smc_reset endp

smc_nmibutton proc
	movzx rax, byte ptr [rdi+1]
	test rax, rax
	jnz no_change
	mov byte ptr [rdx].state.nmi, 1
no_change:

	mov dword ptr [rdx].state.smc_datacount, 0
	mov dword ptr [rdx].state.smc_data, 0
	ret
smc_nmibutton endp

smc_activityled proc
	movzx rax, byte ptr [rdi+1]
	mov dword ptr [rdx].state.smc_led, eax

	mov dword ptr [rdx].state.smc_datacount, 0
	mov dword ptr [rdx].state.smc_data, 0
	ret
smc_activityled endp

smc_keyboard proc
	mov dword ptr [rdx].state.smc_datacount, 0
	mov dword ptr [rdx].state.smc_data, 0
	ret
smc_keyboard endp

smc_donothing proc
	mov dword ptr [rdx].state.smc_datacount, 0
	mov dword ptr [rdx].state.smc_data, 0
	ret
smc_donothing endp
