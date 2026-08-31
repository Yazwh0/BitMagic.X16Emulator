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

LSR_DataReady equ 00000001b
LSR_Empty equ 01000000b

UART_Buffer equ 0
UART_IER equ 1
UART_IIR equ 2
UART_LCR equ 3
UART_MCR equ 4
UART_LSR equ 5
UART_MSR equ 6
UART_SCR equ 7

UART_Divisor_L equ 0
UART_Divisor_H equ 1

NO_DIVISOR equ 00000efffh

uart struct

	zimodem			qword ?			; pointer to a zimodem struct instance (see zimodem.asm)
	io_start		qword ?			; pointer to the start of the io range in memory

	read_index		dword ?
	write_index		dword ?

	buffer			byte 16 dup (?)	; buffer
	buffer_error	byte 16 dup (?)	; buffer

	stop_bits		dword ?
	parity			dword ?
	empty			dword ?

	cpu_ticks		dword ?			; number of ticks per byte. 8000000 / 921600 / (2 + parity + stopbits)

	divisor_latch	dword ?
	divisor			dword ?

	receive_byte	dword ?			; so we can present the byte on a divisor switch
	interrupt_enabled dword ?
uart ends

include zimodem.asm	; must come after uart struct above

calculate_baudrate macro
	local baud_zero, set_value

	push rcx
	mov eax, [rdx].uart.stop_bits
	mov ecx, [rdx].uart.parity
	lea ecx, [rax + rcx + 8+2] ; 1 start + 8 data + 1 mandatory stop
	imul rax, rcx, 8000000

	push rdx
	movzx ecx, word ptr [rdx].uart.divisor	; read before edx is zeroed for the divide
	test ecx, ecx
	jz baud_zero

	; cpu ticks = 8000_000 * frame size * division / 921_600
	imul rax, rcx
	mov ecx, 921600
	xor edx, edx
	div rcx
	jmp set_value
baud_zero:
	mov eax, NO_DIVISOR
set_value:
	pop rdx

	mov [rdx].uart.cpu_ticks, eax
	pop rcx
endm

uart_init proc
	
	mov [rdx].uart.io_start, rax	; address of 0x9fe0
;	mov byte ptr [rax + UART_LSR], LSR_Empty ; probably true
	mov [rdx].uart.empty, 1

	; set a default for now
	mov [rdx].uart.cpu_ticks, NO_DIVISOR
	mov [rdx].uart.read_index, 0
	mov [rdx].uart.write_index, 0

	mov rdx, [rdx].uart.zimodem
	call zimodem_init

	ret

uart_init endp

; ticks at the baud rate of the modem
; pull one byte from zimodem if available
; we use flow control here, so we never overflow. if fifo is full then don't read.
; set the LSR flags depending on the state of the FIFO. we can assume they are correct on entry.
; 
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

	sub rsp, 20h

	mov ebx, [r12].uart.write_index

	; read a byte into the buffer, if there is nothign to read, then head out.
	mov rcx, [r13].zimodem.handle
	call [r13].zimodem.zimodem_host_rx_read	
	cmp eax, -1
	je nothing_returned

	lea edi, [rbx + 1]
	and edi, 16-1

	mov byte ptr [r12].uart.buffer[rbx], al
	mov [r12].uart.write_index, edi

	mov rcx, [r12].uart.io_start
	; if the fifo was empty, then present the new byte
	cmp [r12].uart.empty, 0
	je not_empty

	cmp [r12].uart.divisor_latch, 0
	jne dont_set_buffer
	mov byte ptr [rcx + UART_Buffer], al
dont_set_buffer:
	mov byte ptr [r12].uart.receive_byte, al

not_empty:
	mov [r12].uart.empty, 0
	mov byte ptr [rcx + UART_LSR], LSR_DataReady		; we know all other flags will be clear

nothing_returned:
	; see if there is more data
	mov rcx, [r13].zimodem.handle
	mov [r13].zimodem.data_available, 0
	call [r13].zimodem.zimodem_host_rx_available
	test eax, eax
	jz exit			; nothing available

	mov [r13].zimodem.data_available, 1					; needs to be a constant to avoid race

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

	mov rsp, rbp
	pop rbp

	pop r13
	pop r12

	mov eax, [rdx].uart.cpu_ticks
	ret

fast_exit:

	pop r13
	pop r12

	mov eax, [rdx].uart.cpu_ticks
	ret

uart_tick endp

; in: rdx = pointer to uart struct
;	   al = byte to send
uart_write proc

	cmp [rdx].uart.divisor_latch, 0
	jne set_divisor

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

set_divisor:

	mov byte ptr [rdx].uart.divisor, al
	calculate_baudrate

	ret

uart_write endp

; pull a byte from the buffer if there is one available.
; set the next byte if there is one in the fifo
; if not then set the LSR correctly
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

	; set the new byte in memory
	mov al, byte ptr [[rdx].uart.buffer + rcx]
	mov rcx, [rdx].uart.io_start
	cmp [rdx].uart.divisor_latch, 0
	jne dont_set_buffer
	mov byte ptr [rcx + UART_Buffer], al
dont_set_buffer:
	mov byte ptr [rdx].uart.receive_byte, al

nothing_todo:
	ret

went_empty:
	; set empty and ensure data ready is clear
	mov [rdx].uart.empty, 1
	mov rcx, [rdx].uart.io_start
	and byte ptr [rcx + UART_LSR], NOT LSR_DataReady
	ret

uart_after_read endp

; in: rdx = pointer to the state struct NOT uart struct
uart_nochange proc
	mov byte ptr [rsi + rbx], r12b
	ret
uart_nochange endp

; in: rdx = pointer to the state struct NOT uart struct
uart_dlm_ier_write proc

	movzx eax, byte ptr [rsi + rbx]

	push rdx
	mov rdx, [rdx].state.uart
	cmp [rdx].uart.divisor_latch, 0
	jne set_divisor

	mov [rdx].uart.interrupt_enabled, eax

	pop rdx
	ret

set_divisor:
	mov byte ptr [rdx].uart.divisor + 1, al

	calculate_baudrate

	pop rdx
	ret

uart_dlm_ier_write endp

; in: rdx = pointer to the state struct NOT uart struct
uart_fcr_write proc

	movzx eax, byte ptr [rsi + rbx]
	; preserve the currenct value, FCR is write only
	mov byte ptr [rsi + rbx], r12b



	ret
uart_fcr_write endp

uart_lcr_write proc
	movzx eax, byte ptr [rsi + rbx]

	push rdx
	push rbx

	mov ebx, eax
	shr ebx, 7
	mov rdx, [rdx].state.uart
	mov byte ptr [rdx].uart.divisor_latch, bl

	pop rbx
	pop rdx

	ret
uart_lcr_write endp

uart_mcr_write proc

	movzx eax, byte ptr [rsi + rbx]
;	and al, 00111111b


;	push rdx
;	mov rdx, [rdx].state.uart
;	mov byte ptr [rsi + rbx], al
;	test al, al
;	jnz latch_set

;	mov eax, [rdx].uart.receive_byte
;	push rbx
;	mov rbx, [rdx].uart.io_start
;	mov byte ptr [rbx + UART_Buffer], al

;	mov eax, [rdx].uart.interrupt_enabled
;	mov byte ptr [rbx + UART_IER], al
;	pop rbx

;	pop rdx
;	ret

;latch_set:
;	mov eax, [rdx].uart.divisor
;	mov rbx, [rdx].uart.io_start
;	mov word ptr [rbx + UART_Divisor_L], ax
;	pop rdx
	ret
uart_mcr_write endp

