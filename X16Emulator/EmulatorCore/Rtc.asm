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

; MCP7940N RTC, on I2C at $6F.
;
; The time isn't stepped. It's kept as seconds since 2000-01-01 00:00:00 plus the cpu clock when that was set, and the
; current time is worked out when a register is read, so it costs nothing until it's used:
;   seconds = rtc_seconds + (clock - rtc_base_clock) / RTC_CYCLES
; Writing a register works out the time, changes that field and puts it back.
;
; $00-$06 are the time, $20-$5F the SRAM (NVRAM). Control, trim, alarms and the power fail timestamps ($07-$1F) aren't
; emulated: they read as 0 and writes are ignored.

RTC_CYCLES equ 8000000				; cpu cycles a second
RTC_CENTURY_DAYS equ 36525			; 2000-2099, every 4th year is a leap year

; the time's fields, in an 8 byte buffer at rdi
RTC_SEC equ 0
RTC_MIN equ 1
RTC_HOUR equ 2
RTC_WKDAY equ 3
RTC_DATE equ 4
RTC_MONTH equ 5
RTC_YEAR equ 6

; eax = binary 0-99, returns it as BCD. Uses ecx.
rtc_to_bcd macro
    imul ecx, eax, 205
    shr ecx, 11								; tens
    imul ecx, ecx, 6
    add eax, ecx
endm

; eax = BCD, returns it as binary. Uses ecx.
rtc_from_bcd macro
    mov ecx, eax
    shr ecx, 4
    and eax, 0fh
    imul ecx, ecx, 10
    add eax, ecx
endm

; The time now: eax = seconds since 2000-01-01, ecx = cycles into the current second. Stopped, it's rtc_seconds.
rtc_now macro
    local stopped, done

    cmp dword ptr [rdx].state.rtc_running, 0
    je stopped

    push rdx
    mov rax, r14
    sub rax, [rdx].state.rtc_base_clock
    xor edx, edx
    mov ecx, RTC_CYCLES
    div rcx									; rax = whole seconds, rdx = cycles into the next
    mov rcx, rdx
    pop rdx
    add eax, [rdx].state.rtc_seconds
    jmp done

stopped:
    mov eax, [rdx].state.rtc_seconds
    xor ecx, ecx
done:
endm

; Splits the seconds in eax into the fields at rdi. Uses rax, rbx, rcx, r12, r13.
rtc_split macro
    local year_loop, year_done, month_loop, month_done, not_february

    mov r13d, [rdx].state.rtc_wkday_offset
    push rdx

    xor edx, edx
    mov ecx, 86400
    div ecx
    mov r12d, eax							; days
    mov eax, edx
    xor edx, edx
    mov ecx, 60
    div ecx
    mov byte ptr [rdi + RTC_SEC], dl
    xor edx, edx
    div ecx
    mov byte ptr [rdi + RTC_MIN], dl
    mov byte ptr [rdi + RTC_HOUR], al

    ; the weekday counts on its own, 1 to 7
    lea eax, [r12d + r13d]
    xor edx, edx
    mov ecx, 7
    div ecx
    inc edx
    mov byte ptr [rdi + RTC_WKDAY], dl

    ; years, in blocks of 4 that start with a leap year
    mov eax, r12d
    xor edx, edx
    mov ecx, RTC_CENTURY_DAYS
    div ecx
    mov eax, edx							; day in the century
    xor edx, edx
    mov ecx, 1461
    div ecx									; eax = block, edx = day in it
    lea ebx, [eax * 4]
    mov ecx, 366
year_loop:
    cmp edx, ecx
    jb year_done
    sub edx, ecx
    inc ebx
    mov ecx, 365
    jmp year_loop
year_done:
    mov byte ptr [rdi + RTC_YEAR], bl

    xor r13d, r13d							; 1 in a leap year
    test ebx, 3
    sete r13b
    lea rcx, rtc_month_days
    xor eax, eax
month_loop:
    movzx ebx, byte ptr [rcx + rax]
    cmp eax, 1
    jne not_february
    add ebx, r13d
not_february:
    cmp edx, ebx
    jb month_done
    sub edx, ebx
    inc eax
    jmp month_loop
month_done:
    inc eax
    mov byte ptr [rdi + RTC_MONTH], al
    inc edx
    mov byte ptr [rdi + RTC_DATE], dl

    pop rdx
endm

; Keeps the field at rdi + offset at most max.
rtc_max macro offset, max
    local ok
    cmp byte ptr [rdi + offset], max
    jbe ok
    mov byte ptr [rdi + offset], max
ok:
endm

; Keeps the field at rdi + offset at least 1.
rtc_min1 macro offset
    local ok
    cmp byte ptr [rdi + offset], 1
    jae ok
    mov byte ptr [rdi + offset], 1
ok:
endm

; Makes the fields at rdi a real time, eg after a program writes 31 February. Uses eax, ecx.
rtc_clamp macro
    local not_leap_february, ok
    rtc_max RTC_SEC, 59
    rtc_max RTC_MIN, 59
    rtc_max RTC_HOUR, 23
    rtc_max RTC_YEAR, 99
    rtc_min1 RTC_MONTH
    rtc_max RTC_MONTH, 12
    rtc_min1 RTC_DATE

    movzx eax, byte ptr [rdi + RTC_MONTH]
    lea rcx, rtc_month_days
    movzx ecx, byte ptr [rcx + rax - 1]		; days in the month
    cmp eax, 2
    jne not_leap_february
    test byte ptr [rdi + RTC_YEAR], 3
    jnz not_leap_february
    inc ecx
not_leap_february:
    cmp byte ptr [rdi + RTC_DATE], cl
    jbe ok
    mov byte ptr [rdi + RTC_DATE], cl
ok:
endm

; Joins the fields at rdi back into seconds since 2000-01-01 in eax, and days in r12d. Uses rax, rbx, rcx, r12.
rtc_join macro
    local no_leap_day

    ; 365 days a year, plus a leap day for each earlier year that's a multiple of 4
    movzx eax, byte ptr [rdi + RTC_YEAR]
    imul r12d, eax, 365
    add eax, 3
    shr eax, 2
    add r12d, eax

    ; days before the month, plus this year's leap day from March
    movzx eax, byte ptr [rdi + RTC_MONTH]
    dec eax
    lea rcx, rtc_month_start
    movzx ebx, word ptr [rcx + rax * 2]
    add r12d, ebx
    cmp eax, 2
    jb no_leap_day
    test byte ptr [rdi + RTC_YEAR], 3
    jnz no_leap_day
    inc r12d
no_leap_day:

    movzx eax, byte ptr [rdi + RTC_DATE]
    lea r12d, [r12d + eax - 1]

    imul eax, r12d, 86400
    movzx ebx, byte ptr [rdi + RTC_HOUR]
    imul ebx, ebx, 3600
    add eax, ebx
    movzx ebx, byte ptr [rdi + RTC_MIN]
    imul ebx, ebx, 60
    add eax, ebx
    movzx ebx, byte ptr [rdi + RTC_SEC]
    add eax, ebx
endm

; Moves the register pointer on. It wraps within the time registers ($00-$1F) and within the SRAM ($20-$5F).
rtc_next_register macro
    local wrap_time, store

    mov eax, [rdx].state.rtc_offset
    inc eax
    cmp eax, 20h
    je wrap_time
    cmp eax, 60h
    jne store
    mov eax, 20h
    jmp store
wrap_time:
    xor eax, eax
store:
    and eax, 0ffh
    mov [rdx].state.rtc_offset, eax
endm

; ebx = the register at rtc_offset.
rtc_read_value proc
    mov eax, [rdx].state.rtc_offset
    cmp eax, 20h
    jb time_registers
    cmp eax, 60h
    jae out_of_range

    mov rdi, qword ptr [rdx].state.rtc_nvram_ptr
    movzx ebx, byte ptr [rdi + rax - 20h]
    ret

out_of_range:
    mov ebx, 0ffh
    ret

not_emulated:
    xor ebx, ebx
    ret

time_registers:
    cmp eax, 7
    jae not_emulated

    push rcx
    sub rsp, 8
    mov rdi, rsp
    rtc_now
    rtc_split

    mov eax, [rdx].state.rtc_offset
    lea rcx, read_table
    add rcx, [rcx + rax * 8]
    jmp rcx

read_seconds:
    movzx eax, byte ptr [rdi + RTC_SEC]
    rtc_to_bcd
    mov ebx, [rdx].state.rtc_running		; ST
    shl ebx, 7
    or ebx, eax
    jmp done

read_minutes:
    movzx eax, byte ptr [rdi + RTC_MIN]
    rtc_to_bcd
    mov ebx, eax
    jmp done

read_hours:
    movzx eax, byte ptr [rdi + RTC_HOUR]
    cmp dword ptr [rdx].state.rtc_h24, 0
    je twelve_hour
    rtc_to_bcd
    mov ebx, eax
    jmp done

twelve_hour:
    mov ebx, 40h							; 12 hour mode
    cmp eax, 12
    jb am
    or ebx, 20h								; PM
    sub eax, 12
am:
    test eax, eax
    jnz not_twelve
    mov eax, 12
not_twelve:
    rtc_to_bcd
    or ebx, eax
    jmp done

read_weekday:
    movzx ebx, byte ptr [rdi + RTC_WKDAY]
    mov eax, [rdx].state.rtc_running		; OSCRUN
    shl eax, 5
    or ebx, eax
    mov eax, [rdx].state.rtc_vbaten
    shl eax, 3
    or ebx, eax
    jmp done

read_date:
    movzx eax, byte ptr [rdi + RTC_DATE]
    rtc_to_bcd
    mov ebx, eax
    jmp done

read_month:
    movzx eax, byte ptr [rdi + RTC_MONTH]
    rtc_to_bcd
    mov ebx, eax
    test byte ptr [rdi + RTC_YEAR], 3		; LPYR
    jnz done
    or ebx, 20h
    jmp done

read_year:
    movzx eax, byte ptr [rdi + RTC_YEAR]
    rtc_to_bcd
    mov ebx, eax

done:
    add rsp, 8
    pop rcx
    ret

align 8
read_table:
    qword read_seconds - read_table
    qword read_minutes - read_table
    qword read_hours - read_table
    qword read_weekday - read_table
    qword read_date - read_table
    qword read_month - read_table
    qword read_year - read_table
rtc_read_value endp

; Writes ebx to the register at rtc_offset.
rtc_write_value proc
    mov eax, [rdx].state.rtc_offset
    cmp eax, 20h
    jb time_registers
    cmp eax, 60h
    jae ignore

    mov rdi, qword ptr [rdx].state.rtc_nvram_ptr
    mov byte ptr [rdi + rax - 20h], bl
ignore:
    ret

time_registers:
    cmp eax, 7
    jae ignore

    push rcx
    push rbx								; [rsp + 24] the value
    sub rsp, 8								; [rsp + 16] the fields
    mov rdi, rsp
    rtc_now
    push rcx								; [rsp + 8] cycles into this second
    rtc_split
    movzx eax, byte ptr [rdi + RTC_WKDAY]
    push rax								; [rsp] the weekday, which stays as it is unless it's written

    mov ebx, [rsp + 24]
    mov eax, [rdx].state.rtc_offset
    lea rcx, write_table
    add rcx, [rcx + rax * 8]
    jmp rcx

write_seconds:
    mov eax, ebx
    and eax, 7fh
    rtc_from_bcd
    mov byte ptr [rdi + RTC_SEC], al
    mov qword ptr [rsp + 8], 0				; starts a new second
    shr ebx, 7
    mov [rdx].state.rtc_running, ebx		; ST
    jmp join

write_minutes:
    mov eax, ebx
    and eax, 7fh
    rtc_from_bcd
    mov byte ptr [rdi + RTC_MIN], al
    jmp join

write_hours:
    test ebx, 40h
    jnz write_twelve_hour
    mov dword ptr [rdx].state.rtc_h24, 1
    mov eax, ebx
    and eax, 3fh
    rtc_from_bcd
    mov byte ptr [rdi + RTC_HOUR], al
    jmp join

write_twelve_hour:
    mov dword ptr [rdx].state.rtc_h24, 0
    mov eax, ebx
    and eax, 1fh
    rtc_from_bcd
    cmp eax, 12
    jne not_twelve
    xor eax, eax
not_twelve:
    test ebx, 20h							; PM
    jz am
    add eax, 12
am:
    mov byte ptr [rdi + RTC_HOUR], al
    jmp join

write_weekday:
    mov eax, ebx
    and eax, 7
    jnz weekday_ok
    mov eax, 1
weekday_ok:
    mov [rsp], rax
    shr ebx, 3
    and ebx, 1
    mov [rdx].state.rtc_vbaten, ebx
    jmp join

write_date:
    mov eax, ebx
    and eax, 3fh
    rtc_from_bcd
    mov byte ptr [rdi + RTC_DATE], al
    jmp join

write_month:
    mov eax, ebx
    and eax, 1fh
    rtc_from_bcd
    mov byte ptr [rdi + RTC_MONTH], al
    jmp join

write_year:
    mov eax, ebx
    rtc_from_bcd
    mov byte ptr [rdi + RTC_YEAR], al

join:
    rtc_clamp
    rtc_join
    mov [rdx].state.rtc_seconds, eax

    ; keep the weekday: offset = (weekday - 1 - days) mod 7
    mov eax, r12d
    push rdx
    xor edx, edx
    mov ecx, 7
    div ecx
    mov ecx, edx
    pop rdx
    mov eax, [rsp]
    dec eax
    sub eax, ecx
    add eax, 7
    cmp eax, 7
    jb offset_ok
    sub eax, 7
offset_ok:
    mov [rdx].state.rtc_wkday_offset, eax

    ; keep the part of a second already counted
    mov rax, r14
    sub rax, [rsp + 8]
    mov [rdx].state.rtc_base_clock, rax

    add rsp, 24
    pop rbx
    pop rcx
    ret

align 8
write_table:
    qword write_seconds - write_table
    qword write_minutes - write_table
    qword write_hours - write_table
    qword write_weekday - write_table
    qword write_date - write_table
    qword write_month - write_table
    qword write_year - write_table
rtc_write_value endp

; A byte from the CPU, in rbx. The first after the address is the register pointer, the rest are written to the
; registers from there.
rtc_receive_data proc
    cmp dword ptr [rdx].state.rtc_datacount, 0
    jne register_data

    mov [rdx].state.rtc_offset, ebx
    mov dword ptr [rdx].state.rtc_datacount, 1
    ret

register_data:
    call rtc_write_value
    rtc_next_register
    ret
rtc_receive_data endp

; Stop. The pointer stays, the KERNAL sets it, stops, then starts again to read.
rtc_stop proc
    mov dword ptr [rdx].state.rtc_datacount, 0
    ret
rtc_stop endp

; The next byte to send to the CPU, returned in rbx.
rtc_set_next_write proc
    call rtc_read_value
    rtc_next_register
    ret
rtc_set_next_write endp

rtc_month_days:
    byte 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31

align 2
rtc_month_start:
    word 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334
