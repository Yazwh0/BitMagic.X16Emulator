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

I2C_SMC equ 42h
I2C_RTC equ 6Fh

;	modes:
;	val mode becomes
;	--	0	intial_state
;	11	1	start_1
;	10  2	start_2
;	00	3	read_address
;
;	on a 0->1 change on clock, read data and put into transmit. on 8th, change mode to ack
;

; expects 
; rax currnt value or V_ORA
i2c_handledata proc
	and rax, 11b

	; log the bus
	; -- debug --
	mov r13d, dword ptr [rdx].state.i2c_position
	mov rdi, qword ptr [rdx].state.i2c_buffer_ptr
	mov byte ptr [rdi + r13], al
	add r13d, 1
	and r13d, 3ffh	; 1024 entries
	mov dword ptr [rdx].state.i2c_position, r13d
	; -- ---- --
	
	mov ebx, dword ptr [rdx].state.i2c_previous		; get the last value to check against
	mov dword ptr [rdx].state.i2c_previous, eax		; store for next time
	
	mov r12d, dword ptr [rdx].state.i2c_mode

	; check if we're in a state which requires a reset
	; data changing while clock is high means we need to reset

	mov r13, rbx
	shl r13, 2
	or r13, rax
	cmp r13, 1011b

	jne no_reset

	xor r12, r12
	push rax
	push rbx
	call i2c_stop
	pop rbx
	pop rax

;	bt rax, 1
;	jnc clock_low

;	mov r13, rbx
;	xor r13, rax
;	bt r13, 0		; if 1 we have a change
;	jnc clock_low

;	cmp r12, 2
;	jge clock_low

;	xor r12, r12	; reset

;clock_low:

no_reset:

	; --- debug
	mov r13d, dword ptr [rdx].state.i2c_position
	mov rdi, qword ptr [rdx].state.i2c_buffer_ptr
	mov byte ptr [rdi + r13], r12b
	add r13d, 1
	and r13d, 3ffh	; 1024 entries
	mov dword ptr [rdx].state.i2c_position, r13d
	; --- debug


	lea r13, mode_jump
	add r13, [r13 + r12 * 8]
	jmp r13

align 8
mode_jump:
	; initial state, address read + rw
	qword intial_state					- mode_jump ; 0
	qword start_1						- mode_jump ; 1
	qword start_2						- mode_jump ; 2

	qword read_first_bit				- mode_jump ; start read address
	qword clock_low						- mode_jump ; 4 

	qword read_bit						- mode_jump ; 5 
	qword clock_low						- mode_jump ; 6

	qword read_bit						- mode_jump ; 7
	qword clock_low						- mode_jump ; 8

	qword read_bit						- mode_jump ; 9
	qword clock_low						- mode_jump ; 10

	qword read_bit						- mode_jump ; 11
	qword clock_low						- mode_jump ; 12

	qword read_bit						- mode_jump ; 13
	qword clock_low						- mode_jump ; 14

	qword read_bit						- mode_jump ; 15 - last read into address data
	qword clock_low						- mode_jump ; 16

	qword read_rw_bit					- mode_jump ; 17 - sets rw bit and moves byte from transmit to address

	qword send_address_ack				- mode_jump ; 18 - waits for clock low
	qword clock_high					- mode_jump ; 19
	qword clock_low_afteraddress		- mode_jump ; 20 - end of address sequence, sets the next step

	qword reset_i2c						- mode_jump ; 21

read_byte:	
	; read data from the i2c bus and hand to the device
	qword read_first_bit				- mode_jump ; 22	 
	qword clock_low						- mode_jump ; 23

	qword read_bit						- mode_jump ; 24
	qword clock_low						- mode_jump 

	qword read_bit						- mode_jump ; 26
	qword clock_low						- mode_jump 

	qword read_bit						- mode_jump ; 28
	qword clock_low						- mode_jump					

	qword read_bit						- mode_jump ; 30
	qword clock_low						- mode_jump 

	qword read_bit						- mode_jump ; 32
	qword clock_low						- mode_jump 

	qword read_bit						- mode_jump ; 34
	qword clock_low						- mode_jump 

	qword read_last_bit					- mode_jump ; 36 - calls Process Message

	qword send_dataread_ack				- mode_jump ; 37 - this also waits for clock low
	qword clock_high                    - mode_jump 
	qword clock_low_afterread_ack		- mode_jump ; 39
	qword reset_i2c						- mode_jump ; 40

write_byte:								; writes wait for clock low
	qword write_first_bit				- mode_jump ; 41
	qword clock_high					- mode_jump ; 42

	qword write_bit						- mode_jump ; 43
	qword clock_high					- mode_jump ; 44

	qword write_bit						- mode_jump ; 45
	qword clock_high                    - mode_jump 

	qword write_bit						- mode_jump ; 47
	qword clock_high                    - mode_jump 

	qword write_bit						- mode_jump ; 49
	qword clock_high                    - mode_jump 

	qword write_bit						- mode_jump ; 51
	qword clock_high                    - mode_jump 

	qword write_bit						- mode_jump ; 53
	qword clock_high                    - mode_jump 

	qword write_bit						- mode_jump ; 55
	qword clock_high                    - mode_jump 

	qword clock_low_setdefault			- mode_jump ; 57

	;qword write_last_bit				
	qword read_datawrite_ack			- mode_jump ; 58
	qword clock_low_afterwrite_ack		- mode_jump ; 59, set mode back to 41. A stop from the CPU will reset.

	; should read ack off the line, if there is one goto 41, otherwise reset.
	;qword clock_high					; 59 -ack from cpu
	;qword clock_low

	
	;qword send_datawrite_ack			; 58 - this also waits for clock low
	;qword clock_high					; 59
	;qword clock_low_afterwrite_ack		; 60

	qword reset_i2c						; 60

I2C_READBYTE equ (read_byte - mode_jump) / 8
I2C_WRITEBYTE equ (write_byte - mode_jump) / 8

i2c_handledata endp

i2c_stop proc

	; todo: change once rtc is in
	mov eax, dword ptr [rdx].state.i2c_address
	cmp eax, I2C_RTC
	je rtc
	call smc_stop

	ret
rtc:
	call rtc_stop

	ret
i2c_stop endp

intial_state proc
	cmp rax, 11b
	sete r12b			; if its 11b then move to start_1, otherwise stay at 0
	mov dword ptr [rdx].state.i2c_mode, r12d
	ret
intial_state endp

start_1 proc
	cmp rax, 11b
	je no_change_start_1

	inc r12
	xor rbx, rbx
	cmp rax, 10b
	cmovne r12, rbx		; if its 01b then move to start_2, otherwise back to 0
	mov dword ptr [rdx].state.i2c_mode, r12d

no_change_start_1:
	ret
start_1 endp

start_2 proc
	cmp rax, 10b
	je no_change_start_2

	inc r12
	xor rbx, rbx
	test rax, rax
	cmovnz r12, rbx		; if its 00b then move to read address, otherwise back to 0
	mov dword ptr [rdx].state.i2c_mode, r12d
	mov dword ptr [rdx].state.i2c_transmit, ebx		; clear the data ready to receive
	mov dword ptr [rdx].state.i2c_address, ebx		; clear the address ready to receive

no_change_start_2:
	ret
start_2 endp

read_bit proc
	; clock will be zero entering this state, we wait for clock up, and another stage for clock down
	; when reading data the data can only change when the clock is 0, otherwise we're in an error.
	; take whats in the data when the clock goes from 0 -> 1

	bt rax, 1						; check clock
	jnc read_bit_noclock			; if clock is 0, then the data line can do anything

	mov ebx, dword ptr [rdx].state.i2c_transmit
	and rax, 01b
	shl rbx, 1
	or rbx, rax
	mov dword ptr [rdx].state.i2c_transmit, ebx
	inc dword ptr [rdx].state.i2c_mode
read_bit_noclock:
	ret
read_bit endp
	
; read first bit, so ensure transmit is clear
read_first_bit proc
	; clock will be zero entering this state, we wait for clock up, and another stage for clock down
	; when reading data the data can only change when the clock is 0, otherwise we're in an error.
	; take whats in the data when the clock goes from 0 -> 1

	bt rax, 1						; check clock
	jnc read_bit_noclock			; if clock is 0, then the data line can do anything

	and rax, 01b
	mov dword ptr [rdx].state.i2c_transmit, eax
	inc dword ptr [rdx].state.i2c_mode
read_bit_noclock:
	ret
read_first_bit endp

; read last bit, hand byte to whatever is being sent to
read_last_bit proc
	; clock will be zero entering this state, we wait for clock up, and another stage for clock down
	; when reading data the data can only change when the clock is 0, otherwise we're in an error.
	; take whats in the data when the clock goes from 0 -> 1

	bt rax, 1						; check clock
	jnc read_bit_noclock			; if clock is 0, then the data line can do anything

	mov ebx, dword ptr [rdx].state.i2c_transmit
	and rax, 01b
	shl rbx, 1
	or rbx, rax
	mov dword ptr [rdx].state.i2c_transmit, ebx
	inc dword ptr [rdx].state.i2c_mode

	; only have SMC right now. todo: change to handle the RTC as well.
	mov eax, dword ptr [rdx].state.i2c_address
	cmp eax, I2C_RTC
	je rtc

	jmp smc_receive_data

rtc:
	jmp rtc_receive_data

read_bit_noclock:
	ret
read_last_bit endp

; wait for clock to go low, data cant change%
clock_low proc
	bt rax, 1
	jc clock_is_high

	inc dword ptr [rdx].state.i2c_mode
	ret
clock_is_high:
	; if the clock is high, data line can't change
	mov r13d, dword ptr [rdx].state.i2c_mode
	;xor r12, r12
	mov r12, 1
	and rax, 1b
	
	and rbx, 1b
	xor rax, rbx
	cmovnz r13, r12		; if the bit is set, then data has changed which is an error so reset
	mov dword ptr [rdx].state.i2c_mode, r13d

	jnz i2c_stop		; this will call ret.

	ret
clock_low endp

; wait for clock to go low, data cant change
clock_low_setdefault proc
	bt rax, 1
	jc clock_is_high

	inc dword ptr [rdx].state.i2c_mode

	or byte ptr [rdx].state.via_register_a_invalue, 01b	; set data to high

	ret
clock_is_high:
	; if the clock is high, data line can't change
	mov r13d, dword ptr [rdx].state.i2c_mode
	;xor r12, r12
	mov r12, 1
	and rax, 1b
	
	and rbx, 1b
	xor rax, rbx
	cmovnz r13, r12		; if the bit is set, then data has changed which is an error so reset
	mov dword ptr [rdx].state.i2c_mode, r13d

	jnz i2c_stop		; this will call ret.

	ret
clock_low_setdefault endp

; wait for the clock to go low, then set the in-data high
; set the next mode to I2C_READBYTE
clock_low_afteraddress proc
	bt rax, 1
	jc clock_is_high

	or byte ptr [rdx].state.via_register_a_invalue, 01b	; set data to high (opposite of ack)

	mov rax, I2C_READBYTE
	mov rbx, I2C_WRITEBYTE
	mov r13d, dword ptr [rdx].state.i2c_readwrite
	test r13, r13
	cmovnz rax, rbx

	mov dword ptr [rdx].state.i2c_mode, eax
	ret
clock_is_high:
	; if the clock is high, data line can't change
	mov r13d, dword ptr [rdx].state.i2c_mode
	xor r12, r12
	and rax, 1b
	
	and rbx, 1b
	xor rax, rbx
	cmovnz r13, r12		; if the bit is set, then data has changed which is an error so reset
	mov dword ptr [rdx].state.i2c_mode, r13d

	jnz i2c_stop		; this will call ret.

	ret
clock_low_afteraddress endp

; wait for the clock to go low, then set the in-data high
clock_low_afterread_ack proc
	bt rax, 1
	jc clock_is_high

	or byte ptr [rdx].state.via_register_a_invalue, 01b	; set data to high to ack the read
	mov dword ptr [rdx].state.i2c_mode, I2C_READBYTE	; go back to reading -- if its stop condition we will error occordingly
	ret
clock_is_high:
	; if the clock is high, data line can't change
	mov r13d, dword ptr [rdx].state.i2c_mode
	xor r12, r12
	and rax, 1b
	
	and rbx, 1b
	xor rax, rbx
	cmovnz r13, r12		; if the bit is set, then data has changed which is an error so reset
	mov dword ptr [rdx].state.i2c_mode, r13d

	jnz i2c_stop		; this will call ret.

	ret
clock_low_afterread_ack endp

; wait for the clock to go low, then set the in-data high
clock_low_afterwrite_ack proc
	bt rax, 1
	jc clock_is_high

	mov dword ptr [rdx].state.i2c_mode, I2C_WRITEBYTE
	ret
clock_is_high:
	; if the clock is high, data line can't change
	mov r13d, dword ptr [rdx].state.i2c_mode
	xor r12, r12
	and rax, 1b
	
	and rbx, 1b
	xor rax, rbx
	cmovnz r13, r12		; if the bit is set, then data has changed which is an error so reset
	mov dword ptr [rdx].state.i2c_mode, r13d

	jnz i2c_stop		; this will call ret.

	ret
clock_low_afterwrite_ack endp

; wait for the clock to go high, data can change until its high
clock_high proc
	bt rax, 1
	jc clock_is_high

	ret
clock_is_high:
	; if the clock is high, data line can't change
	mov r13d, dword ptr [rdx].state.i2c_mode
	inc r13
	xor r12, r12
	and rax, 1b
	and rbx, 1b
	xor rax, rbx
	cmovnz r13, r12		; if the bit is set, then data has changed which is an error so reset
	mov dword ptr [rdx].state.i2c_mode, r13d

	jnz i2c_stop		; this will call ret.

	ret
clock_high endp

read_rw_bit proc
	bt rax, 1						; check clock
	jnc read_bw_bit_noclock			; if clock is 0, then the data line can do anything

	and rax, 01b
	mov dword ptr [rdx].state.i2c_readwrite, eax
	inc dword ptr [rdx].state.i2c_mode

	mov eax, dword ptr [rdx].state.i2c_transmit
	mov dword ptr [rdx].state.i2c_address, eax

read_bw_bit_noclock:
	ret
read_rw_bit endp

send_address_ack proc
	bt rax, 1			; check clock
	jc noclock			; if clock is 0, then we can write data

	mov ebx, dword ptr [rdx].state.i2c_transmit
	cmp ebx, I2C_RTC
	je known_address
	cmp ebx, I2C_SMC
	jne unknown_address

known_address:
	movzx rax, byte ptr [rdx].state.via_register_a_invalue
	and rax, 11111110b	; set data low
	mov byte ptr [rdx].state.via_register_a_invalue, al

	; update ORA, PRA
	movzx r13, byte ptr [rdx].state.via_register_a_outvalue
	movzx rdi, byte ptr [rsi+V_DDRA]
	and r13, rdi					; keep outbound values
	xor rdi, 0ffh
	and rax, rdi					; values in
	or r13, rax						; merge values inbound w/ outbound
	mov byte ptr [rsi+V_PRA], r13b	; store
	mov byte ptr [rsi+V_ORA], r13b

	; ---- debug ----
	mov r12d, dword ptr [rdx].state.i2c_position
	mov rdi, qword ptr [rdx].state.i2c_buffer_ptr
	mov byte ptr [rdi + r12], r13b
	add r12d, 1
	and r12d, 3ffh	; 1024 entries
	mov dword ptr [rdx].state.i2c_position, r12d
	mov byte ptr [rdi + r12], 0
	add r12d, 1
	and r12d, 3ffh	; 1024 entries
	mov dword ptr [rdx].state.i2c_position, r12d
	; ---- ----- ----

	and r13, 11b
	mov dword ptr [rdx].state.i2c_previous, r13d		; store for next time


	inc dword ptr [rdx].state.i2c_mode
	ret
unknown_address:

	movzx rax, byte ptr [rdx].state.via_register_a_invalue
	or rax, 1b	; set data high (indicating no ack)
	mov byte ptr [rdx].state.via_register_a_invalue, al

	; update ORA, PRA
	movzx r13, byte ptr [rdx].state.via_register_a_outvalue
	movzx rdi, byte ptr [rsi+V_DDRA]
	and r13, rdi					; keep outbound values
	xor rdi, 0ffh
	and rax, rdi					; values in
	or r13, rax						; merge values inbound w/ outbound
	mov byte ptr [rsi+V_PRA], r13b	; store
	mov byte ptr [rsi+V_ORA], r13b
	
	; ---- debug ----
	mov r12d, dword ptr [rdx].state.i2c_position
	mov rdi, qword ptr [rdx].state.i2c_buffer_ptr
	mov byte ptr [rdi + r12], r13b
	add r12d, 1
	and r12d, 3ffh	; 1024 entries
	mov dword ptr [rdx].state.i2c_position, r12d
	mov byte ptr [rdi + r12], 0
	add r12d, 1
	and r12d, 3ffh	; 1024 entries
	mov dword ptr [rdx].state.i2c_position, r12d
	; ---- ----- ----

	and r13, 11b
	mov dword ptr [rdx].state.i2c_previous, r13d		; store for next time


	; wait for start condition
	xor rbx, rbx
	mov dword ptr [rdx].state.i2c_mode, ebx
	ret

noclock:
	ret
send_address_ack endp

; Ack after data has been send to the I2C device
; As its inbound, we have to ack it.
send_dataread_ack proc
	bt rax, 1			; check clock
	jc noclock			; if clock is 0, then we can write data

	movzx rax, byte ptr [rdx].state.via_register_a_invalue
	and rax, 11111110b	; set data low to ack
	mov byte ptr [rdx].state.via_register_a_invalue, al

	; update ORA, PRA
	movzx r13, byte ptr [rdx].state.via_register_a_outvalue
	movzx rdi, byte ptr [rsi+V_DDRA]
	and r13, rdi					; keep outbound values
	xor rdi, 0ffh
	and rax, rdi					; values in
	or r13, rax						; merge values inbound w/ outbound
	mov byte ptr [rsi+V_PRA], r13b	; store
	mov byte ptr [rsi+V_ORA], r13b

	; ---- debug ----
	mov r12d, dword ptr [rdx].state.i2c_position
	mov rdi, qword ptr [rdx].state.i2c_buffer_ptr
	mov byte ptr [rdi + r12], r13b
	add r12d, 1
	and r12d, 3ffh	; 1024 entries
	mov dword ptr [rdx].state.i2c_position, r12d
	mov byte ptr [rdi + r12], 0
	add r12d, 1
	and r12d, 3ffh	; 1024 entries
	mov dword ptr [rdx].state.i2c_position, r12d
	; ---- ----- ----

	and r13, 11b
	mov dword ptr [rdx].state.i2c_previous, r13d		; store for next time


	inc dword ptr [rdx].state.i2c_mode
	ret

noclock:
	ret
send_dataread_ack endp

; read ack from the line, as we're sending to the I2C master, we need to read the ack
read_datawrite_ack proc
	bt rax, 1			; check clock
	jnc noclock			; only read when clock is high

	mov r12d, dword ptr [rdx].state.i2c_mode
	inc r12
	xor r13, r13

	bt rax, 0
	cmovc r12, r13		; if set ,then nack, so set mode back to 0
	mov dword ptr [rdx].state.i2c_mode, r12d
	
	ret

	; todo: check to detect if there is more data
	movzx rax, byte ptr [rdx].state.via_register_a_invalue
	and rax, 11111110b	; set data low to ack
	mov ebx, dword ptr [rdx].state.smc_keyboard_readnodata
	or rax, rbx			; is 1 for no data so noack. 0 for data, which is ack
	mov byte ptr [rdx].state.via_register_a_invalue, al

	; update ORA, PRA
	movzx r13, byte ptr [rdx].state.via_register_a_outvalue
	movzx rdi, byte ptr [rsi+V_DDRA]
	and r13, rdi					; keep outbound values
	xor rdi, 0ffh
	and rax, rdi					; values in
	or r13, rax						; merge values inbound w/ outbound
	mov byte ptr [rsi+V_PRA], r13b	; store
	mov byte ptr [rsi+V_ORA], r13b

	; ---- debug ----
	mov r12d, dword ptr [rdx].state.i2c_position
	mov rdi, qword ptr [rdx].state.i2c_buffer_ptr
	mov byte ptr [rdi + r12], r13b
	add r12d, 1
	and r12d, 3ffh	; 1024 entries
	mov dword ptr [rdx].state.i2c_position, r12d
	mov byte ptr [rdi + r12], 0
	add r12d, 1
	and r12d, 3ffh	; 1024 entries
	mov dword ptr [rdx].state.i2c_position, r12d
	; ---- ----- ----

	and r13, 11b
	mov dword ptr [rdx].state.i2c_previous, r13d		; store for next time


noclock:
	ret
read_datawrite_ack endp

write_first_bit proc
	bt rax, 1						; check clock
	jc write_bit_clockhigh			; can only write when the clock is 0

	lea r13, smc_set_next_write
	lea rdi, rtc_set_next_write
	mov eax, [rdx].state.i2c_address
	cmp eax, I2C_SMC
	cmove rdi, r13					; if SMC, then load that address
	call rdi						; returns the data in ebx
	
	shl ebx, 1
	mov dword ptr [rdx].state.i2c_transmit, ebx
	and ebx, 100000000b ; bit 8 because we've shifted
	setnz bl

	movzx rax, byte ptr [rdx].state.via_register_a_invalue
	and rax, 11111110b
	or rax, rbx			; or on the data

	mov byte ptr [rdx].state.via_register_a_invalue, al

	; update ORA, PRA
	movzx r13, byte ptr [rdx].state.via_register_a_outvalue
	movzx rdi, byte ptr [rsi+V_DDRA]
	and r13, rdi					; keep outbound values
	xor rdi, 0ffh
	and rax, rdi					; values in
	or r13, rax						; merge values inbound w/ outbound
	mov byte ptr [rsi+V_PRA], r13b	; store
	mov byte ptr [rsi+V_ORA], r13b

	; ---- debug ----
	mov r12d, dword ptr [rdx].state.i2c_position
	mov rdi, qword ptr [rdx].state.i2c_buffer_ptr
	mov byte ptr [rdi + r12], r13b
	add r12d, 1
	and r12d, 3ffh	; 1024 entries
	mov dword ptr [rdx].state.i2c_position, r12d
	mov byte ptr [rdi + r12], 0
	add r12d, 1
	and r12d, 3ffh	; 1024 entries
	mov dword ptr [rdx].state.i2c_position, r12d
	; ---- ----- ----

	and r13, 11b
	mov dword ptr [rdx].state.i2c_previous, r13d		; store for next time


	inc dword ptr [rdx].state.i2c_mode

write_bit_clockhigh:
	; if the clock is high, data line can't change
	mov r13d, dword ptr [rdx].state.i2c_mode
	;xor r12, r12
	mov r12, 1
	and rax, 1b
	
	and rbx, 1b
	xor rax, rbx
	cmovnz r13, r12		; if the bit is set, then data has changed which is an error so reset
	mov dword ptr [rdx].state.i2c_mode, r13d

	jnz i2c_stop		; this will call ret.

	ret
write_first_bit endp

write_bit proc
	bt rax, 1						; check clock
	jc write_bit_clockhigh			; can only write when the clock is 0

	mov ebx ,dword ptr [rdx].state.i2c_transmit
	shl ebx, 1
	mov dword ptr [rdx].state.i2c_transmit, ebx
	and ebx, 100000000b ; bit 8 because we've shifted
	setnz bl

	movzx rax, byte ptr [rdx].state.via_register_a_invalue
	and rax, 11111110b
	or rax, rbx			; or on the data

	mov byte ptr [rdx].state.via_register_a_invalue, al

	; update ORA, PRA
	movzx r13, byte ptr [rdx].state.via_register_a_outvalue
	movzx rdi, byte ptr [rsi+V_DDRA]
	and r13, rdi					; keep outbound values
	xor rdi, 0ffh
	and rax, rdi					; values in
	or r13, rax						; merge values inbound w/ outbound
	mov byte ptr [rsi+V_PRA], r13b	; store
	mov byte ptr [rsi+V_ORA], r13b

	; ---- debug ----
	mov r12d, dword ptr [rdx].state.i2c_position
	mov rdi, qword ptr [rdx].state.i2c_buffer_ptr
	mov byte ptr [rdi + r12], r13b
	add r12d, 1
	and r12d, 3ffh	; 1024 entries
	mov dword ptr [rdx].state.i2c_position, r12d
	mov byte ptr [rdi + r12], 0
	add r12d, 1
	and r12d, 3ffh	; 1024 entries
	mov dword ptr [rdx].state.i2c_position, r12d
	; ---- ----- ----

	and r13, 11b
	mov dword ptr [rdx].state.i2c_previous, r13d		; store for next time


	inc dword ptr [rdx].state.i2c_mode

write_bit_clockhigh:
	; if the clock is high, data line can't change
	mov r13d, dword ptr [rdx].state.i2c_mode
	;xor r12, r12
	mov r12, 1
	and rax, 1b
	
	and rbx, 1b
	xor rax, rbx
	cmovnz r13, r12		; if the bit is set, then data has changed which is an error so reset
	mov dword ptr [rdx].state.i2c_mode, r13d

	jnz i2c_stop		; this will call ret.

	ret
write_bit endp


write_last_bit proc
	bt rax, 1						; check clock
	jc write_bit_clockhigh			; can only write when the clock is 0

	mov ebx ,dword ptr [rdx].state.i2c_transmit
	shl ebx, 1
	mov dword ptr [rdx].state.i2c_transmit, ebx
	and ebx, 100000000b ; bit 8 because we've shifted
	setnz bl

	movzx rax, byte ptr [rdx].state.via_register_a_invalue
	and rax, 11111110b
	or rax, rbx			; or on the data

	mov byte ptr [rdx].state.via_register_a_invalue, al

	; update ORA, PRA
	movzx r13, byte ptr [rdx].state.via_register_a_outvalue
	movzx rdi, byte ptr [rsi+V_DDRA]
	and r13, rdi					; keep outbound values
	xor rdi, 0ffh
	and rax, rdi					; values in
	or r13, rax						; merge values inbound w/ outbound
	mov byte ptr [rsi+V_PRA], r13b	; store
	mov byte ptr [rsi+V_ORA], r13b

	; ---- debug ----
	mov r12d, dword ptr [rdx].state.i2c_position
	mov rdi, qword ptr [rdx].state.i2c_buffer_ptr
	mov byte ptr [rdi + r12], r13b
	add r12d, 1
	and r12d, 3ffh	; 1024 entries
	mov dword ptr [rdx].state.i2c_position, r12d
	mov byte ptr [rdi + r12], 0
	add r12d, 1
	and r12d, 3ffh	; 1024 entries
	mov dword ptr [rdx].state.i2c_position, r12d
	; ---- ----- ----

	and r13, 11b
	mov dword ptr [rdx].state.i2c_previous, r13d		; store for next time

	inc dword ptr [rdx].state.i2c_mode

	; update pointer if necessary
	mov eax, dword ptr [rdx].state.i2c_datatotransmit
	test eax, eax
	jz dont_complete
	call smc_complete_write
	dont_complete:

write_bit_clockhigh:
	; if the clock is high, data line can't change
	mov r13d, dword ptr [rdx].state.i2c_mode
	;xor r12, r12
	mov r12, 1
	and rax, 1b
	
	and rbx, 1b
	xor rax, rbx
	cmovnz r13, r12		; if the bit is set, then data has changed which is an error so reset
	mov dword ptr [rdx].state.i2c_mode, r13d

	jnz i2c_stop		; this will call ret.

	ret
write_last_bit endp

reset_i2c proc
	xor rbx, rbx
	mov dword ptr [rdx].state.i2c_mode, ebx
	ret	
reset_i2c endp
