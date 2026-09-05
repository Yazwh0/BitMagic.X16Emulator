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

; This file talks directly to zimodem_host.dll's C ABI (see zimodem_host.h in the
; BitMagic.ZiModem submodule, native/wrapper/include) -- EXTERN-linked against
; zimodem_host.lib. Not yet wired into the project: EmulatorCore.vcxproj still needs
; zimodem_host.h's include dir and zimodem_host.lib added, and something needs to call
; zimodem_init once at startup (Core.asm's state.initial_startup branch is where every
; other one-time init in this codebase lives) and zimodem_write_serial/
; zimodem_serial_available/zimodem_read_serial/zimodem_set_pin need entries in Io.asm's
; dispatch tables (or calls from whatever register model the UART emulation builds).
; Included from Uart.asm, after uart_state is declared there.

; All native-interop state for the one zimodem_handle this process will ever have
; (zimodem_host.h's own one-instance-per-process constraint), reached via
; state.uart -> uart_state.zimodem -> this struct. data_dir doubles as the
; zimodem_host_config* passed to zimodem_host_create() -- that native struct is just
; { const char* data_dir; }, so a pointer to this one field has an identical layout.
;
; The eight zimodem_host entry points are NOT cached here -- unlike state.step_ym/
; write_register_ym/sleep/get_ticks elsewhere in this codebase (which exist as struct
; fields because those are internal, non-exported C++ functions with no linkable symbol
; for asm to EXTERN against -- the only way to hand asm a callable address for them is
; for C++ to resolve it at runtime and hand it over via state), zimodem_host_* are real
; ZIMODEM_API exports with proper entries in zimodem_host.lib. EXTERN + linking against
; that .lib already gives every proc below a directly callable symbol, so there's
; nothing to resolve or store -- see the EXTERN block and the calls further down.
zimodem struct
	handle						qword ?
	data_dir					qword ?

	zimodem_host_create			qword ?
	zimodem_host_set_callbacks	qword ?
	zimodem_host_start			qword ?
	zimodem_host_write_serial	qword ?
	zimodem_host_rx_available	qword ?
	zimodem_host_rx_read		qword ?
	zimodem_host_set_pin		qword ?
	zimodem_host_destroy		qword ?

	data_available				dword ?

	data_tx						dword ?			; used to send data to zimodem
	data_tx_error				dword ?

	; --- line config, pushed by zimodem_on_line_config (the 4th zimodem_host_set_callbacks
	;     callback) from the modem's background thread ---
	line_baud					dword ?			; bits per second; 0 until the modem's setup() has run
	line_data_bits				dword ?			; 5..8
	line_parity					dword ?			; 0 = none, 1 = odd, 2 = even (ZIMODEM_PARITY_*)
	line_stop_bits_x10			dword ?			; stop bits * 10: 10, 15, or 20

	; total bits on the wire per byte: 1 start + data bits + (parity ? 1 : 0) + stop bits.
	; Derived from the four line_* fields above by zimodem_on_line_config; 0 until setup() runs.
	; For the usual 115200 8N1 this is 10.
	data_width					dword ?

zimodem ends

.CODE

; in: rdx = pointer to struct
;
; Self-aligning frame: the emulator core does not keep RSP 16-byte aligned at its
; internal call sites, so we can't assume the ABI-standard entry alignment. `and rsp, -16`
; forces it; rbp anchors the original RSP so we can unwind (can't `add` a constant back --
; the `and` removes an unknown 0 or 8). rbp is non-volatile, so push/pop preserves it for
; the caller too. After alignment, `sub rsp, 30h` (multiple of 16) gives 20h shadow + the
; arg5/arg6 home slots at [rsp+20h]/[rsp+28h] and keeps RSP 16-aligned at every call below.
zimodem_init proc
	push rbp
	mov rbp, rsp
	and rsp, -16
	sub rsp, 30h

	mov r12, rdx

	; init
	lea rcx, [r12].zimodem.data_dir
	call [r12].zimodem.zimodem_host_create
	test rax, rax
	jz zimodem_init_failed
	mov [r12].zimodem.handle, rax

	; Power-on line defaults (115200 8N1, confirmed on hardware). Seeds the struct for the
	; window between here and the modem thread's setup() running begin() -- which fires
	; zimodem_on_line_config with these same values. data_width = 1 start + 8 data + 0
	; parity + 1 stop.
	mov [r12].zimodem.line_baud, 115200
	mov [r12].zimodem.line_data_bits, 8
	mov [r12].zimodem.line_parity, 0
	mov [r12].zimodem.line_stop_bits_x10, 10
	mov [r12].zimodem.data_width, 10

	; callbacks
	mov rcx, rax					; handle
	lea rdx, zimodem_on_serial_out
	xor r8, r8						; on_signal -- not wired up yet
	xor r9, r9						; on_log -- not wired up yet
	lea rax, zimodem_on_line_config
	mov qword ptr [rsp + 20h], rax	; on_line_config (arg5)
	mov qword ptr [rsp + 28h], r12	; user_context (arg6) -- the pointer to the struct
	call [r12].zimodem.zimodem_host_set_callbacks

	mov rcx, [r12].zimodem.handle
	call [r12].zimodem.zimodem_host_start			; eax already holds start()'s result (0 = success)

	mov rsp, rbp
	pop rbp
	ret

zimodem_init_failed:
	mov	eax, 1
	mov rsp, rbp
	pop rbp
	ret

zimodem_init endp

; ---------------------------------------------------------------------------------
; zimodem_on_serial_out -- the C ABI callback zimodem_host calls on ITS OWN background
; thread (see zimodem_host.h) whenever the modem has queued at least one byte for the
; host. 
;
; void zimodem_on_serial_out(void* user_context)
; in: rcx = user_context
; ---------------------------------------------------------------------------------
zimodem_on_serial_out proc
	mov [rcx].zimodem.data_available, 1
	ret
zimodem_on_serial_out endp

; ---------------------------------------------------------------------------------
; zimodem_on_line_config -- the C ABI callback zimodem_host calls on ITS OWN background
; thread whenever the vendored firmware changes baud / data bits / parity / stop bits
; (an AT config, an ATSxx write, or the power-on default). Registered as the 4th
; callback of zimodem_host_set_callbacks.
;
; void zimodem_on_line_config(void* user_context, int baud, int data_bits,
;                             int parity, int stop_bits_x10)
; in: rcx = user_context (the zimodem struct)
;     edx = baud
;     r8d = data_bits
;     r9d = parity
;     [rsp+28h] = stop_bits_x10   ; arg5: [rsp]=return addr, [rsp+8..20h]=shadow, [rsp+28h]=arg5
;
; Leaf: aligned stores + a small integer calc, no frame -- like zimodem_on_serial_out.
; Values are read back by the emulator core (UART) to check them against its own
; divisor/framing. r10d/r11d/eax/edx are all volatile under Win64, so scratching them
; here is free (rcx is the only input we must preserve past the stores).
; ---------------------------------------------------------------------------------
zimodem_on_line_config proc
	mov eax, dword ptr [rsp + 28h]			; stop_bits_x10 (arg5, past the shadow space)
	mov [rcx].zimodem.line_baud, edx
	mov [rcx].zimodem.line_data_bits, r8d
	mov [rcx].zimodem.line_parity, r9d
	mov [rcx].zimodem.line_stop_bits_x10, eax

	; data_width = 1 start + data_bits + (parity ? 1 : 0) + round(stop_bits_x10 / 10)
	lea r11d, [r9d + 1]
	shr r11d, 1								; parity {0 none, 1 odd, 2 even} -> {0, 1, 1}
	lea r10d, [r8d + r11d + 1]				; start bit + data bits + parity bit
	add eax, 5								; round-to-nearest for the /10 (1.5 stop bits -> 2)
	xor edx, edx
	mov r11d, 10
	div r11d								; eax = (stop_bits_x10 + 5) / 10  -> 1 or 2
	add r10d, eax							; stop bits
	mov [rcx].zimodem.data_width, r10d
	ret
zimodem_on_line_config endp

;;; ---------------------------------------------------------------------------------
;;; zimodem_write_serial -- call from Io.asm's write dispatch for the UART's TX register.
;;; Follows this codebase's I/O-handler convention (see ym_write_data in Ym.asm): the byte
;;; the CPU wrote is already sitting at [rsi + rbx] by the time this runs. Uses the
;;; emulator's hot-loop register convention, so the full save/restore dance wraps the
;;; actual native call.
;;; ---------------------------------------------------------------------------------
;zimodem_write_serial proc
;	mov cl, byte ptr [rsi + rbx]	; byte to send
;	mov zimodem_storage.tx_byte, cl

;	pushf
;	push rsi
;	push r15
;	push r14
;	push r13
;	push r12
;	push r11
;	push r10
;	push r9
;	push r8
;	push rdx
;	push rcx

;	; 12 pushes above = 96 bytes (a multiple of 16), so rsp's parity is unchanged from
;	; entry (8 mod 16). 0x28 (40 = 32 shadow + 8 padding) is the smallest allocation that
;	; both covers the mandatory shadow space and restores 16-byte alignment for the call.
;	sub rsp, 28h
;	mov rcx, zimodem_storage.handle
;	lea rdx, zimodem_storage.tx_byte
;	mov r8, 1
;	call zimodem_host_write_serial
;	add rsp, 28h

;	pop rcx
;	pop rdx
;	pop r8
;	pop r9
;	pop r10
;	pop r11
;	pop r12
;	pop r13
;	pop r14
;	pop r15
;	pop rsi
;	popf

;	ret
;zimodem_write_serial endp

;; ---------------------------------------------------------------------------------
;; zimodem_serial_available -- call from Io.asm's read dispatch for the UART's status
;; register. Real native call now (zimodem_host_rx_available polls the modem's actual
;; queue), not a local field read -- there's no local buffer to check anymore.
;; out: al = 1 if a byte is waiting, 0 otherwise
;; ---------------------------------------------------------------------------------
;zimodem_serial_available proc
;	pushf
;	push rsi
;	push r15
;	push r14
;	push r13
;	push r12
;	push r11
;	push r10
;	push r9
;	push r8
;	push rdx
;	push rcx

;	; Same accounting as zimodem_write_serial: 12 pushes above (96 bytes, a multiple of
;	; 16) leave rsp's parity unchanged from entry (8 mod 16), so 0x28 restores alignment.
;	sub rsp, 28h
;	mov rcx, zimodem_storage.handle
;	call zimodem_host_rx_available
;	add rsp, 28h
;	; al now holds the result -- none of the pops below touch rax, so it survives them.

;	pop rcx
;	pop rdx
;	pop r8
;	pop r9
;	pop r10
;	pop r11
;	pop r12
;	pop r13
;	pop r14
;	pop r15
;	pop rsi
;	popf

;	mov byte ptr [rsi + rbx], al
;	ret
;zimodem_serial_available endp

;; ---------------------------------------------------------------------------------
;; zimodem_read_serial -- call from Io.asm's read dispatch for the UART's data register.
;; Real native call now (zimodem_host_rx_read dequeues from the modem's actual queue),
;; not a local field read.
;; out: al = the byte read (native side returns -1/0xFF if nothing was actually waiting)
;; ---------------------------------------------------------------------------------
;zimodem_read_serial proc
;	pushf
;	push rsi
;	push r15
;	push r14
;	push r13
;	push r12
;	push r11
;	push r10
;	push r9
;	push r8
;	push rdx
;	push rcx

;	sub rsp, 28h
;	mov rcx, zimodem_storage.handle
;	call zimodem_host_rx_read
;	add rsp, 28h

;	pop rcx
;	pop rdx
;	pop r8
;	pop r9
;	pop r10
;	pop r11
;	pop r12
;	pop r13
;	pop r14
;	pop r15
;	pop rsi
;	popf

;	mov byte ptr [rsi + rbx], al
;	ret
;zimodem_read_serial endp

;; ---------------------------------------------------------------------------------
;; zimodem_set_pin -- drive a modem control pin (ZIMODEM_PIN_* in zimodem_host.h), e.g.
;; CTS for flow control. No established I/O-dispatch precedent to match (unlike
;; write_serial/read_serial, which mirror ym_write_data's [rsi+rbx] convention) -- this
;; takes its arguments directly in registers instead.
;; in: cl = pin number, dl = value (ZIMODEM_PIN_ACTIVE=0/LOW or ZIMODEM_PIN_INACTIVE=1/HIGH)
;; ---------------------------------------------------------------------------------
;zimodem_set_pin proc
;	pushf
;	push rsi
;	push r15
;	push r14
;	push r13
;	push r12
;	push r11
;	push r10
;	push r9
;	push r8
;	push rdx
;	push rcx

;	; cl/dl are unaffected by the pushes above (push copies a register's value onto the
;	; stack, it doesn't clear the register) -- stash them in r9/r10 now, before rcx/rdx
;	; get overwritten with the call's own arguments below. r9/r10 are safe scratch here:
;	; a 3-arg call only needs rcx/rdx/r8.
;	movzx r9d, cl
;	movzx r10d, dl

;	; Same accounting as zimodem_write_serial: 12 pushes above (96 bytes, a multiple of
;	; 16) leave rsp's parity unchanged from entry (8 mod 16), so 0x28 restores alignment.
;	sub rsp, 28h
;	mov rcx, zimodem_storage.handle
;	mov rdx, r9
;	mov r8, r10
;	call zimodem_host_set_pin
;	add rsp, 28h

;	pop rcx
;	pop rdx
;	pop r8
;	pop r9
;	pop r10
;	pop r11
;	pop r12
;	pop r13
;	pop r14
;	pop r15
;	pop rsi
;	popf

;	ret
;zimodem_set_pin endp

