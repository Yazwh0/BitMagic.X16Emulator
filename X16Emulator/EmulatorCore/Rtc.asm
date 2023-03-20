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
	ret	
rtc_receive_data endp

; called when a stp siginal is received, so can process the received data -- check this is how the RTC works (could be different to the SMC)
rtc_stop proc
	ret
rtc_stop endp

; return rbx as data to transmit
rtc_set_next_write proc
	mov ebx, 0hff
	ret
rtc_set_next_write endp
