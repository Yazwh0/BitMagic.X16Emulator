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

uart struct

	zimodem			qword ?			; pointer to a zimodem struct instance (see zimodem.asm)
	memory_output	qword ?			; pointer to where a byte should be written in the emulated memory

	read_index		dword ?
	write_index		dword ?

	buffer			byte 16 dup (?)	; buffer
	buffer_error	byte 16 dup (?)	; buffer

	stop_bits		dword ?
	parity			dword ?
	baud_rate		dword ?
	empty			dword ?

	cpu_ticks		dword ?			; number of ticks per byte. 8000000 / 921600 / (2 + parity + stopbits)

uart ends

include zimodem.asm	; must come after uart struct above

uart_init proc
	
	mov [rdx].uart.memory_output, rax	; address of 0x9fe0

	; set a default for now
	mov [rdx].uart.cpu_ticks, 64

	mov [rdx].uart.empty, 1

	mov rdx, [rdx].uart.zimodem
	call zimodem_init

	ret

uart_init endp

uart_tick proc

	push r12
	push r13

	mov r12, rdx
	mov r13, [r12].uart.zimodem
	cmp [r13].zimodem.data_available, 0
	jz fast_exit						; nothing queued from the modem

	cmp [r12].uart.empty, 0
	jne slow_path						; empty -> guaranteed room

	mov eax, [r12].uart.write_index
	cmp eax, [r12].uart.read_index
	je fast_exit						; not empty & write==read -> full, retry next tick

slow_path:
	push rbp
	mov  rbp, rsp
	and  rsp, -16

	push rbx
	push rcx
	push rdx
	push rdi
	push r8
	push r9
	push r10
	push r11

	sub  rsp, 20h

	mov ebx, [r12].uart.write_index

	; read a byte into the buffer
	mov rcx, [r13].zimodem.handle
	call [r13].zimodem.zimodem_host_rx_read	
	cmp eax, -1
	je no_data

	lea edi, [rbx + 1]
	and edi, 16-1

	mov byte ptr [r12].uart.buffer[rbx], al
	mov [r12].uart.write_index, edi

	cmp [r12].uart.empty, 1
	jne skip_output

	mov rcx, [r12].uart.memory_output
	mov byte ptr [rcx], al

skip_output:
	mov [r12].uart.empty, 0

no_data:
	mov rcx, [r13].zimodem.handle
	mov [r13].zimodem.data_available, 0
	call [r13].zimodem.zimodem_host_rx_available
	test eax, eax
	jz exit

	mov [r13].zimodem.data_available, 1 ; needs to be a constant to avoid race

exit:
	add rsp, 20h
	pop r11
	pop r10
	pop r9
	pop r8
	pop rdi
	pop rdx
	pop rcx
	pop rbx

	mov  rsp, rbp
	pop  rbp

fast_exit:

	pop r13
	pop r12

	mov eax, [rdx].uart.cpu_ticks
	ret

uart_tick endp

; in: rdx = pointer to struct
;	   al = byte to send
uart_write proc

	push r12						; non-volatile, clobbered below

	push rbp						; self-aligning frame -- core doesn't keep RSP 16-aligned
	mov  rbp, rsp
	and  rsp, -16
	sub  rsp, 40h					; 20h shadow + 20h to save r8-r11

	mov  [rsp+20h], r8				; the call below trashes r8-r11 (6502 A/X/Y/PC)
	mov  [rsp+28h], r9
	mov  [rsp+30h], r10
	mov  [rsp+38h], r11

	mov r12, [rdx].uart.zimodem

	mov [r12].zimodem.data_tx, eax
	mov rcx, [r12].zimodem.handle
	lea rdx, [r12].zimodem.data_tx
	mov r8, 1
	call [r12].zimodem.zimodem_host_write_serial ; call write directly so we dont waste cycles

	mov [r12].zimodem.data_tx_error, eax

	mov  r8,  [rsp+20h]
	mov  r9,  [rsp+28h]
	mov  r10, [rsp+30h]
	mov  r11, [rsp+38h]

	mov  rsp, rbp
	pop  rbp
	pop  r12
	ret

uart_write endp

; pull a byte from the buffer if there is one available.
; in: rdx = pointer to struct
uart_after_read proc

	cmp [rdx].uart.empty, 0
	jne nothing_todo					; empty == 1 -> FIFO already empty, nothing to advance

	; advance past the byte the CPU just consumed
	mov ecx, [rdx].uart.read_index
	inc ecx
	and ecx, 16-1
	mov [rdx].uart.read_index, ecx

	cmp ecx, [rdx].uart.write_index
	je went_empty						; caught up to write -> now empty

	; still data: present buffer[read_index] at the mapped register
	mov al, byte ptr [rdx].uart.buffer[rcx]
	mov rcx, [rdx].uart.memory_output
	mov byte ptr [rcx], al

	ret

went_empty:
	mov [rdx].uart.empty, 1
nothing_todo:
	ret

uart_after_read endp