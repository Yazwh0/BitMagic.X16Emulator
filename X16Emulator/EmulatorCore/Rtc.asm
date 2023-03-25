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

; read the state of the i2c bus and set state as required
; expects 
; rbx :- message (alternativley in i2c_transmit)
rtc_receive_data proc
	mov eax, dword ptr [rdx].state.rtc_datacount
	cmp eax, 2
	jg overflow

	mov dword ptr [rdx].state.rtc_offset, ebx
	lea rdi, [rdx].state.rtc_data
	mov byte ptr [rdi + rax], bl
	inc eax
	mov dword ptr [rdx].state.rtc_datacount, eax

overflow:
	ret
rtc_receive_data endp

; called when a stp siginal is received, so can process the received data -- check this is how the RTC works (could be different to the SMC)
rtc_stop proc
	; todo, this should check read / write.

	lea rdi, [rdx].state.rtc_data
	movzx rbx, byte ptr [rdi]						; get offset
	mov dword ptr [rdx].state.rtc_offset, ebx		; store for write operations (eg keyb\mouse)

	mov eax, dword ptr [rdx].state.i2c_readwrite
	test eax, eax
	jnz reading

	mov eax, dword ptr [rdx].state.rtc_datacount
	cmp eax, 2h
	jl not_enough_data

	movzx rbx, byte ptr [rdi]						; get offset
	cmp ebx, 20h
	jl clock

	; this is a nvram
	movzx rax, byte ptr [rdi + 1] ; value

	; rax = value, rbx = offset
	; need to add 0x20 to the offset as the memory is mapped from there.
	mov rdi, qword ptr [rdx].state.rtc_nvram_ptr
	mov byte ptr [rdi + rbx - 20h], al

reading:
not_enough_data:
	mov dword ptr [rdx].state.rtc_datacount, 0
	mov dword ptr [rdx].state.rtc_data, 0
	ret

clock:

	mov dword ptr [rdx].state.rtc_datacount, 0
	mov dword ptr [rdx].state.rtc_data, 0
	ret
rtc_stop endp

; return rbx as data to transmit
rtc_set_next_write proc
	mov eax, dword ptr [rdx].state.rtc_offset

	cmp eax, 20h
	jl clock
	cmp eax, 60h
	jge out_of_bounds

	mov rdi, qword ptr [rdx].state.rtc_nvram_ptr
	movzx rbx, byte ptr [rdi + rax - 20h]
	ret

clock:
	; todo

out_of_bounds:
	mov ebx, 0ffh
	ret
rtc_set_next_write endp
