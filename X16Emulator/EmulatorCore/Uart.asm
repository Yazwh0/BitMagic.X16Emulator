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
	
	; set a default for now
	mov [rdx].uart.cpu_ticks, 64

	mov [rdx].uart.empty, 1

	mov rdx, [rdx].uart.zimodem
	call zimodem_init

	ret

uart_init endp

uart_tick proc

	mov r12, rdx
	mov r13, [r12].uart.zimodem
	cmp [r13].zimodem.data_available, 0
	jz fast_exit

	push rdx
	push r12 ; has to be even
	sub  rsp, 28h

	mov ebx, [r12].uart.write_index

	cmp [r12].uart.empty, 0
	jne do_read

	cmp ebx, [r12].uart.read_index
	je exit		; we're full

do_read:
	; read a byte into the buffer
	mov rcx, [r13].zimodem.handle
	call [r13].zimodem.zimodem_host_rx_read	
	cmp eax, -1
	je no_data

	lea edi, [rbx + 1]
	and edi, 16-1

	mov byte ptr [r12].uart.buffer[rbx], al
	mov [r12].uart.write_index, edi
	mov [r12].uart.empty, 0

no_data:
	mov rcx, [r13].zimodem.handle
	mov [r13].zimodem.data_available, 0
	call [r13].zimodem.zimodem_host_rx_available
	test eax, eax
	jz exit

	mov [r13].zimodem.data_available, 1 ; needs to be a constant to avoid race

exit:
	add rsp, 28h
	pop r12
	pop rdx

fast_exit:
	mov eax, [rdx].uart.cpu_ticks
	ret

uart_tick endp

; in: rdx = pointer to struct
;	   al = byte to send
uart_write proc

	sub rsp, 28h

	mov r12, [rdx].uart.zimodem

	mov [r12].zimodem.data_tx, eax
	mov rcx, [r12].zimodem.handle
	lea rdx, [r12].zimodem.data_tx
	mov r8, 1
	call [r12].zimodem.zimodem_host_write_serial ; call write directly so we dont waste cycles

	mov [r12].zimodem.data_tx_error, eax

	add rsp, 28h
	ret

uart_write endp

; pull a byte from the buffer if there is one available.
; in: rdx = pointer to struct
uart_after_read proc

	cmp [rdx].uart.empty, 0
	je exit

	mov r12d, [rdx].uart.read_index
	mov al, [[rdx].uart.buffer + r12]

	inc r12
	and r12, 16-1
	mov [rdx].uart.read_index, r12d
	cmp r12d, [rdx].uart.write_index
	jne exit

	mov [rdx].uart.empty, 1

exit:
	; need to set the value in memory
	ret

empty:
	; should write empty value???
	ret

uart_after_read endp