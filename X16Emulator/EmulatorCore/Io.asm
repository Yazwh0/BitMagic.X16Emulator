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

;
; r13: address in io space +1
;

io_afterwrite proc
	dec r13
	lea rax, io_registers_write
	add rax, [rax + r13 * 8]
	jmp rax
io_afterwrite endp

io_afterread proc
	dec r13
	lea rax, io_registers_read
	add rax, [rax + r13 * 8]
	jmp rax
io_afterread endp

io_afterreadwrite proc
	dec r13
	lea rax, io_registers_readwrite
	add rax, [rax + r13 * 8]
	jmp rax
io_afterreadwrite endp

io_r_readmemory proc
	ret
io_r_readmemory endp

io_rw_readmemory proc
	ret
io_rw_readmemory endp

io_w_unsupported proc
	ret
io_w_unsupported endp

io_cantwrite proc
	mov byte ptr [rsi + rbx], 0
	ret	
io_cantwrite endp

io_cantwrite_9f41 proc
	mov byte ptr [rsi + rbx], 0
	ret	
io_cantwrite_9f41 endp

io_registers_read:
	io_r_9f00 qword io_r_readmemory - io_registers_read
	io_r_9f01 qword io_r_readmemory - io_registers_read
	io_r_9f02 qword io_r_readmemory - io_registers_read
	io_r_9f03 qword io_r_readmemory - io_registers_read
	io_r_9f04 qword via_timer1_counter_l_read - io_registers_read
	io_r_9f05 qword io_r_readmemory - io_registers_read
	io_r_9f06 qword io_r_readmemory - io_registers_read
	io_r_9f07 qword io_r_readmemory - io_registers_read
	io_r_9f08 qword io_r_readmemory - io_registers_read
	io_r_9f09 qword io_r_readmemory - io_registers_read
	io_r_9f0a qword io_r_readmemory - io_registers_read
	io_r_9f0b qword io_r_readmemory - io_registers_read
	io_r_9f0c qword io_r_readmemory - io_registers_read
	io_r_9f0d qword io_r_readmemory - io_registers_read
	io_r_9f0e qword io_r_readmemory - io_registers_read
	io_r_9f0f qword io_r_readmemory - io_registers_read

	; Unused
	io_r_9f10 qword io_r_readmemory - io_registers_read
	io_r_9f11 qword io_r_readmemory - io_registers_read
	io_r_9f12 qword io_r_readmemory - io_registers_read
	io_r_9f13 qword io_r_readmemory - io_registers_read
	io_r_9f14 qword io_r_readmemory - io_registers_read
	io_r_9f15 qword io_r_readmemory - io_registers_read
	io_r_9f16 qword io_r_readmemory - io_registers_read
	io_r_9f17 qword io_r_readmemory - io_registers_read
	io_r_9f18 qword io_r_readmemory - io_registers_read
	io_r_9f19 qword io_r_readmemory - io_registers_read
	io_r_9f1a qword io_r_readmemory - io_registers_read
	io_r_9f1b qword io_r_readmemory - io_registers_read
	io_r_9f1c qword io_r_readmemory - io_registers_read
	io_r_9f1d qword io_r_readmemory - io_registers_read
	io_r_9f1e qword io_r_readmemory - io_registers_read
	io_r_9f1f qword io_r_readmemory - io_registers_read

	vera_r_9f20 qword io_r_readmemory - io_registers_read
	vera_r_9f21 qword io_r_readmemory - io_registers_read
	vera_r_9f22 qword io_r_readmemory - io_registers_read
	vera_r_9f23 qword vera_afterread - io_registers_read
	vera_r_9f24 qword vera_afterread - io_registers_read
	vera_r_9f25 qword io_r_readmemory - io_registers_read
	vera_r_9f26 qword io_r_readmemory - io_registers_read
	vera_r_9f27 qword io_r_readmemory - io_registers_read
	vera_r_9f28 qword io_r_readmemory - io_registers_read
	vera_r_9f29 qword io_r_readmemory - io_registers_read
	vera_r_9f2a qword io_r_readmemory - io_registers_read
	vera_r_9f2b qword io_r_readmemory - io_registers_read
	vera_r_9f2c qword io_r_readmemory - io_registers_read
	vera_r_9f2d qword io_r_readmemory - io_registers_read
	vera_r_9f2e qword io_r_readmemory - io_registers_read
	vera_r_9f2f qword io_r_readmemory - io_registers_read
	vera_r_9f30 qword io_r_readmemory - io_registers_read
	vera_r_9f31 qword io_r_readmemory - io_registers_read
	vera_r_9f32 qword io_r_readmemory - io_registers_read
	vera_r_9f33 qword io_r_readmemory - io_registers_read
	vera_r_9f34 qword io_r_readmemory - io_registers_read
	vera_r_9f35 qword io_r_readmemory - io_registers_read
	vera_r_9f36 qword io_r_readmemory - io_registers_read
	vera_r_9f37 qword io_r_readmemory - io_registers_read
	vera_r_9f38 qword io_r_readmemory - io_registers_read
	vera_r_9f39 qword io_r_readmemory - io_registers_read
	vera_r_9f3a qword io_r_readmemory - io_registers_read
	vera_r_9f3b qword io_r_readmemory - io_registers_read
	vera_r_9f3c qword io_r_readmemory - io_registers_read
	vera_r_9f3d qword io_r_readmemory - io_registers_read
	vera_r_9f3e qword io_r_readmemory - io_registers_read
	vera_r_9f3f qword io_r_readmemory - io_registers_read

	ym_r_9f40 qword io_r_readmemory - io_registers_read
	ym_r_9f41 qword io_r_readmemory - io_registers_read
	io_r_9f42 qword io_r_readmemory - io_registers_read
	io_r_9f43 qword io_r_readmemory - io_registers_read
	io_r_9f44 qword io_r_readmemory - io_registers_read
	io_r_9f45 qword io_r_readmemory - io_registers_read
	io_r_9f46 qword io_r_readmemory - io_registers_read
	io_r_9f47 qword io_r_readmemory - io_registers_read
	io_r_9f48 qword io_r_readmemory - io_registers_read
	io_r_9f49 qword io_r_readmemory - io_registers_read
	io_r_9f4a qword io_r_readmemory - io_registers_read
	io_r_9f4b qword io_r_readmemory - io_registers_read
	io_r_9f4c qword io_r_readmemory - io_registers_read
	io_r_9f4d qword io_r_readmemory - io_registers_read
	io_r_9f4e qword io_r_readmemory - io_registers_read
	io_r_9f4f qword io_r_readmemory - io_registers_read
	io_r_9f50 qword io_r_readmemory - io_registers_read
	io_r_9f51 qword io_r_readmemory - io_registers_read
	io_r_9f52 qword io_r_readmemory - io_registers_read
	io_r_9f53 qword io_r_readmemory - io_registers_read
	io_r_9f54 qword io_r_readmemory - io_registers_read
	io_r_9f55 qword io_r_readmemory - io_registers_read
	io_r_9f56 qword io_r_readmemory - io_registers_read
	io_r_9f57 qword io_r_readmemory - io_registers_read
	io_r_9f58 qword io_r_readmemory - io_registers_read
	io_r_9f59 qword io_r_readmemory - io_registers_read
	io_r_9f5a qword io_r_readmemory - io_registers_read
	io_r_9f5b qword io_r_readmemory - io_registers_read
	io_r_9f5c qword io_r_readmemory - io_registers_read
	io_r_9f5d qword io_r_readmemory - io_registers_read
	io_r_9f5e qword io_r_readmemory - io_registers_read
	io_r_9f5f qword io_r_readmemory - io_registers_read
	io_r_9f60 qword io_r_readmemory - io_registers_read
	io_r_9f61 qword io_r_readmemory - io_registers_read
	io_r_9f62 qword io_r_readmemory - io_registers_read
	io_r_9f63 qword io_r_readmemory - io_registers_read
	io_r_9f64 qword io_r_readmemory - io_registers_read
	io_r_9f65 qword io_r_readmemory - io_registers_read
	io_r_9f66 qword io_r_readmemory - io_registers_read
	io_r_9f67 qword io_r_readmemory - io_registers_read
	io_r_9f68 qword io_r_readmemory - io_registers_read
	io_r_9f69 qword io_r_readmemory - io_registers_read
	io_r_9f6a qword io_r_readmemory - io_registers_read
	io_r_9f6b qword io_r_readmemory - io_registers_read
	io_r_9f6c qword io_r_readmemory - io_registers_read
	io_r_9f6d qword io_r_readmemory - io_registers_read
	io_r_9f6e qword io_r_readmemory - io_registers_read
	io_r_9f6f qword io_r_readmemory - io_registers_read
	io_r_9f70 qword io_r_readmemory - io_registers_read
	io_r_9f71 qword io_r_readmemory - io_registers_read
	io_r_9f72 qword io_r_readmemory - io_registers_read
	io_r_9f73 qword io_r_readmemory - io_registers_read
	io_r_9f74 qword io_r_readmemory - io_registers_read
	io_r_9f75 qword io_r_readmemory - io_registers_read
	io_r_9f76 qword io_r_readmemory - io_registers_read
	io_r_9f77 qword io_r_readmemory - io_registers_read
	io_r_9f78 qword io_r_readmemory - io_registers_read
	io_r_9f79 qword io_r_readmemory - io_registers_read
	io_r_9f7a qword io_r_readmemory - io_registers_read
	io_r_9f7b qword io_r_readmemory - io_registers_read
	io_r_9f7c qword io_r_readmemory - io_registers_read
	io_r_9f7d qword io_r_readmemory - io_registers_read
	io_r_9f7e qword io_r_readmemory - io_registers_read
	io_r_9f7f qword io_r_readmemory - io_registers_read
	io_r_9f80 qword io_r_readmemory - io_registers_read
	io_r_9f81 qword io_r_readmemory - io_registers_read
	io_r_9f82 qword io_r_readmemory - io_registers_read
	io_r_9f83 qword io_r_readmemory - io_registers_read
	io_r_9f84 qword io_r_readmemory - io_registers_read
	io_r_9f85 qword io_r_readmemory - io_registers_read
	io_r_9f86 qword io_r_readmemory - io_registers_read
	io_r_9f87 qword io_r_readmemory - io_registers_read
	io_r_9f88 qword io_r_readmemory - io_registers_read
	io_r_9f89 qword io_r_readmemory - io_registers_read
	io_r_9f8a qword io_r_readmemory - io_registers_read
	io_r_9f8b qword io_r_readmemory - io_registers_read
	io_r_9f8c qword io_r_readmemory - io_registers_read
	io_r_9f8d qword io_r_readmemory - io_registers_read
	io_r_9f8e qword io_r_readmemory - io_registers_read
	io_r_9f8f qword io_r_readmemory - io_registers_read
	io_r_9f90 qword io_r_readmemory - io_registers_read
	io_r_9f91 qword io_r_readmemory - io_registers_read
	io_r_9f92 qword io_r_readmemory - io_registers_read
	io_r_9f93 qword io_r_readmemory - io_registers_read
	io_r_9f94 qword io_r_readmemory - io_registers_read
	io_r_9f95 qword io_r_readmemory - io_registers_read
	io_r_9f96 qword io_r_readmemory - io_registers_read
	io_r_9f97 qword io_r_readmemory - io_registers_read
	io_r_9f98 qword io_r_readmemory - io_registers_read
	io_r_9f99 qword io_r_readmemory - io_registers_read
	io_r_9f9a qword io_r_readmemory - io_registers_read
	io_r_9f9b qword io_r_readmemory - io_registers_read
	io_r_9f9c qword io_r_readmemory - io_registers_read
	io_r_9f9d qword io_r_readmemory - io_registers_read
	io_r_9f9e qword io_r_readmemory - io_registers_read
	io_r_9f9f qword io_r_readmemory - io_registers_read
	io_r_9fa0 qword io_r_readmemory - io_registers_read
	io_r_9fa1 qword io_r_readmemory - io_registers_read
	io_r_9fa2 qword io_r_readmemory - io_registers_read
	io_r_9fa3 qword io_r_readmemory - io_registers_read
	io_r_9fa4 qword io_r_readmemory - io_registers_read
	io_r_9fa5 qword io_r_readmemory - io_registers_read
	io_r_9fa6 qword io_r_readmemory - io_registers_read
	io_r_9fa7 qword io_r_readmemory - io_registers_read
	io_r_9fa8 qword io_r_readmemory - io_registers_read
	io_r_9fa9 qword io_r_readmemory - io_registers_read
	io_r_9faa qword io_r_readmemory - io_registers_read
	io_r_9fab qword io_r_readmemory - io_registers_read
	io_r_9fac qword io_r_readmemory - io_registers_read
	io_r_9fad qword io_r_readmemory - io_registers_read
	io_r_9fae qword io_r_readmemory - io_registers_read
	io_r_9faf qword io_r_readmemory - io_registers_read
	io_r_9fb0 qword io_r_readmemory - io_registers_read
	io_r_9fb1 qword io_r_readmemory - io_registers_read
	io_r_9fb2 qword io_r_readmemory - io_registers_read
	io_r_9fb3 qword io_r_readmemory - io_registers_read
	io_r_9fb4 qword io_r_readmemory - io_registers_read
	io_r_9fb5 qword io_r_readmemory - io_registers_read
	io_r_9fb6 qword io_r_readmemory - io_registers_read
	io_r_9fb7 qword io_r_readmemory - io_registers_read
	io_r_9fb8 qword io_r_readmemory - io_registers_read
	io_r_9fb9 qword io_r_readmemory - io_registers_read
	io_r_9fba qword io_r_readmemory - io_registers_read
	io_r_9fbb qword io_r_readmemory - io_registers_read
	io_r_9fbc qword io_r_readmemory - io_registers_read
	io_r_9fbd qword io_r_readmemory - io_registers_read
	io_r_9fbe qword io_r_readmemory - io_registers_read
	io_r_9fbf qword io_r_readmemory - io_registers_read
	io_r_9fc0 qword io_r_readmemory - io_registers_read
	io_r_9fc1 qword io_r_readmemory - io_registers_read
	io_r_9fc2 qword io_r_readmemory - io_registers_read
	io_r_9fc3 qword io_r_readmemory - io_registers_read
	io_r_9fc4 qword io_r_readmemory - io_registers_read
	io_r_9fc5 qword io_r_readmemory - io_registers_read
	io_r_9fc6 qword io_r_readmemory - io_registers_read
	io_r_9fc7 qword io_r_readmemory - io_registers_read
	io_r_9fc8 qword io_r_readmemory - io_registers_read
	io_r_9fc9 qword io_r_readmemory - io_registers_read
	io_r_9fca qword io_r_readmemory - io_registers_read
	io_r_9fcb qword io_r_readmemory - io_registers_read
	io_r_9fcc qword io_r_readmemory - io_registers_read
	io_r_9fcd qword io_r_readmemory - io_registers_read
	io_r_9fce qword io_r_readmemory - io_registers_read
	io_r_9fcf qword io_r_readmemory - io_registers_read
	io_r_9fd0 qword io_r_readmemory - io_registers_read
	io_r_9fd1 qword io_r_readmemory - io_registers_read
	io_r_9fd2 qword io_r_readmemory - io_registers_read
	io_r_9fd3 qword io_r_readmemory - io_registers_read
	io_r_9fd4 qword io_r_readmemory - io_registers_read
	io_r_9fd5 qword io_r_readmemory - io_registers_read
	io_r_9fd6 qword io_r_readmemory - io_registers_read
	io_r_9fd7 qword io_r_readmemory - io_registers_read
	io_r_9fd8 qword io_r_readmemory - io_registers_read
	io_r_9fd9 qword io_r_readmemory - io_registers_read
	io_r_9fda qword io_r_readmemory - io_registers_read
	io_r_9fdb qword io_r_readmemory - io_registers_read
	io_r_9fdc qword io_r_readmemory - io_registers_read
	io_r_9fdd qword io_r_readmemory - io_registers_read
	io_r_9fde qword io_r_readmemory - io_registers_read
	io_r_9fdf qword io_r_readmemory - io_registers_read
	io_r_9fe0 qword io_r_readmemory - io_registers_read
	io_r_9fe1 qword io_r_readmemory - io_registers_read
	io_r_9fe2 qword io_r_readmemory - io_registers_read
	io_r_9fe3 qword io_r_readmemory - io_registers_read
	io_r_9fe4 qword io_r_readmemory - io_registers_read
	io_r_9fe5 qword io_r_readmemory - io_registers_read
	io_r_9fe6 qword io_r_readmemory - io_registers_read
	io_r_9fe7 qword io_r_readmemory - io_registers_read
	io_r_9fe8 qword io_r_readmemory - io_registers_read
	io_r_9fe9 qword io_r_readmemory - io_registers_read
	io_r_9fea qword io_r_readmemory - io_registers_read
	io_r_9feb qword io_r_readmemory - io_registers_read
	io_r_9fec qword io_r_readmemory - io_registers_read
	io_r_9fed qword io_r_readmemory - io_registers_read
	io_r_9fee qword io_r_readmemory - io_registers_read
	io_r_9fef qword io_r_readmemory - io_registers_read
	io_r_9ff0 qword io_r_readmemory - io_registers_read
	io_r_9ff1 qword io_r_readmemory - io_registers_read
	io_r_9ff2 qword io_r_readmemory - io_registers_read
	io_r_9ff3 qword io_r_readmemory - io_registers_read
	io_r_9ff4 qword io_r_readmemory - io_registers_read
	io_r_9ff5 qword io_r_readmemory - io_registers_read
	io_r_9ff6 qword io_r_readmemory - io_registers_read
	io_r_9ff7 qword io_r_readmemory - io_registers_read
	io_r_9ff8 qword io_r_readmemory - io_registers_read
	io_r_9ff9 qword io_r_readmemory - io_registers_read
	io_r_9ffa qword io_r_readmemory - io_registers_read
	io_r_9ffb qword io_r_readmemory - io_registers_read
	io_r_9ffc qword io_r_readmemory - io_registers_read
	io_r_9ffd qword io_r_readmemory - io_registers_read
	io_r_9ffe qword io_r_readmemory - io_registers_read
	io_r_9fff qword io_r_readmemory - io_registers_read



io_registers_readwrite:
	io_rw_9f00 qword via_prb - io_registers_readwrite
	io_rw_9f01 qword via_pra - io_registers_readwrite
	io_rw_9f02 qword io_rw_readmemory - io_registers_readwrite
	io_rw_9f03 qword via_dra - io_registers_readwrite
	io_rw_9f04 qword via_timer1_counter_l - io_registers_readwrite
	io_rw_9f05 qword via_timer1_counter_h - io_registers_readwrite
	io_rw_9f06 qword via_timer1_latch_l - io_registers_readwrite
	io_rw_9f07 qword via_timer1_latch_h - io_registers_readwrite
	io_rw_9f08 qword via_timer2_latch_l - io_registers_readwrite
	io_rw_9f09 qword via_timer2_latch_h - io_registers_readwrite
	io_rw_9f0a qword io_rw_readmemory - io_registers_readwrite
	io_rw_9f0b qword via_acl - io_registers_readwrite
	io_rw_9f0c qword io_rw_readmemory - io_registers_readwrite
	io_rw_9f0d qword via_ifr - io_registers_readwrite
	io_rw_9f0e qword via_ier - io_registers_readwrite
	io_rw_9f0f qword via_pra - io_registers_readwrite

	; Unused
	io_rw_9f10 qword io_rw_readmemory - io_registers_readwrite
	io_rw_9f11 qword io_rw_readmemory - io_registers_readwrite
	io_rw_9f12 qword io_rw_readmemory - io_registers_readwrite
	io_rw_9f13 qword io_rw_readmemory - io_registers_readwrite
	io_rw_9f14 qword io_rw_readmemory - io_registers_readwrite
	io_rw_9f15 qword io_rw_readmemory - io_registers_readwrite
	io_rw_9f16 qword io_rw_readmemory - io_registers_readwrite
	io_rw_9f17 qword io_rw_readmemory - io_registers_readwrite
	io_rw_9f18 qword io_rw_readmemory - io_registers_readwrite
	io_rw_9f19 qword io_rw_readmemory - io_registers_readwrite
	io_rw_9f1a qword io_rw_readmemory - io_registers_readwrite
	io_rw_9f1b qword io_rw_readmemory - io_registers_readwrite
	io_rw_9f1c qword io_rw_readmemory - io_registers_readwrite
	io_rw_9f1d qword io_rw_readmemory - io_registers_readwrite
	io_rw_9f1e qword io_rw_readmemory - io_registers_readwrite
	io_rw_9f1f qword io_rw_readmemory - io_registers_readwrite

	vera_rw_9f20 qword vera_update_addrl - io_registers_readwrite
	vera_rw_9f21 qword vera_update_addrm - io_registers_readwrite
	vera_rw_9f22 qword vera_update_addrh - io_registers_readwrite
	vera_rw_9f23 qword vera_afterreadwrite - io_registers_readwrite
	vera_rw_9f24 qword vera_afterreadwrite - io_registers_readwrite
	vera_rw_9f25 qword vera_update_ctrl - io_registers_readwrite
	vera_rw_9f26 qword vera_update_ien - io_registers_readwrite
	vera_rw_9f27 qword vera_update_isr - io_registers_readwrite
	vera_rw_9f28 qword vera_update_irqline_l - io_registers_readwrite
	vera_rw_9f29 qword vera_update_9f29 - io_registers_readwrite
	vera_rw_9f2a qword vera_update_9f2a - io_registers_readwrite
	vera_rw_9f2b qword vera_update_9f2b - io_registers_readwrite
	vera_rw_9f2c qword vera_update_9f2c - io_registers_readwrite
	vera_rw_9f2d qword vera_update_l0config - io_registers_readwrite
	vera_rw_9f2e qword vera_update_l0mapbase - io_registers_readwrite
	vera_rw_9f2f qword vera_update_l0tilebase - io_registers_readwrite
	vera_rw_9f30 qword vera_update_l0hscroll_l - io_registers_readwrite
	vera_rw_9f31 qword vera_update_l0hscroll_h - io_registers_readwrite
	vera_rw_9f32 qword vera_update_l0vscroll_l - io_registers_readwrite
	vera_rw_9f33 qword vera_update_l0vscroll_h - io_registers_readwrite
	vera_rw_9f34 qword vera_update_l1config - io_registers_readwrite
	vera_rw_9f35 qword vera_update_l1mapbase - io_registers_readwrite
	vera_rw_9f36 qword vera_update_l1tilebase - io_registers_readwrite
	vera_rw_9f37 qword vera_update_l1hscroll_l - io_registers_readwrite
	vera_rw_9f38 qword vera_update_l1hscroll_h - io_registers_readwrite
	vera_rw_9f39 qword vera_update_l1vscroll_l - io_registers_readwrite
	vera_rw_9f3a qword vera_update_l1vscroll_h - io_registers_readwrite
	vera_rw_9f3b qword vera_update_audioctrl - io_registers_readwrite
	vera_rw_9f3c qword vera_update_audiorate - io_registers_readwrite
	vera_rw_9f3d qword vera_update_audiodata - io_registers_readwrite
	vera_rw_9f3e qword vera_update_spi_data - io_registers_readwrite
	vera_rw_9f3f qword vera_update_spi_ctrl - io_registers_readwrite


	ym_rw_9f40 qword io_cantwrite - io_registers_readwrite
	ym_rw_9f41 qword io_cantwrite_9f41 - io_registers_readwrite
	io_rw_9f42 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f43 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f44 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f45 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f46 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f47 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f48 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f49 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f4a qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f4b qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f4c qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f4d qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f4e qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f4f qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f50 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f51 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f52 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f53 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f54 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f55 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f56 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f57 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f58 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f59 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f5a qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f5b qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f5c qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f5d qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f5e qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f5f qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f60 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f61 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f62 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f63 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f64 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f65 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f66 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f67 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f68 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f69 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f6a qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f6b qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f6c qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f6d qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f6e qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f6f qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f70 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f71 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f72 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f73 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f74 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f75 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f76 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f77 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f78 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f79 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f7a qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f7b qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f7c qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f7d qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f7e qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f7f qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f80 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f81 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f82 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f83 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f84 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f85 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f86 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f87 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f88 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f89 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f8a qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f8b qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f8c qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f8d qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f8e qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f8f qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f90 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f91 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f92 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f93 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f94 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f95 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f96 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f97 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f98 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f99 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f9a qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f9b qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f9c qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f9d qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f9e qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9f9f qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fa0 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fa1 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fa2 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fa3 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fa4 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fa5 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fa6 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fa7 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fa8 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fa9 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9faa qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fab qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fac qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fad qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fae qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9faf qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fb0 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fb1 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fb2 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fb3 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fb4 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fb5 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fb6 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fb7 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fb8 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fb9 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fba qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fbb qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fbc qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fbd qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fbe qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fbf qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fc0 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fc1 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fc2 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fc3 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fc4 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fc5 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fc6 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fc7 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fc8 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fc9 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fca qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fcb qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fcc qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fcd qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fce qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fcf qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fd0 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fd1 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fd2 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fd3 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fd4 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fd5 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fd6 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fd7 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fd8 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fd9 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fda qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fdb qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fdc qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fdd qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fde qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fdf qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fe0 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fe1 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fe2 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fe3 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fe4 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fe5 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fe6 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fe7 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fe8 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fe9 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fea qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9feb qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fec qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fed qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fee qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fef qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9ff0 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9ff1 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9ff2 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9ff3 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9ff4 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9ff5 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9ff6 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9ff7 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9ff8 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9ff9 qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9ffa qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9ffb qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9ffc qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9ffd qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9ffe qword io_rw_readmemory -  io_registers_readwrite
	io_rw_9fff qword io_rw_readmemory -  io_registers_readwrite

io_registers_write:
	; VIA1
	io_w_9f00 qword via_prb - io_registers_write
	io_w_9f01 qword via_pra - io_registers_write
	io_w_9f02 qword io_w_unsupported - io_registers_write
	io_w_9f03 qword via_dra - io_registers_write
	io_w_9f04 qword via_timer1_counter_l - io_registers_write
	io_w_9f05 qword via_timer1_counter_h - io_registers_write
	io_w_9f06 qword via_timer1_latch_l - io_registers_write
	io_w_9f07 qword via_timer1_latch_h - io_registers_write
	io_w_9f08 qword via_timer2_latch_l - io_registers_write
	io_w_9f09 qword via_timer2_latch_h - io_registers_write
	io_w_9f0a qword io_w_unsupported - io_registers_write
	io_w_9f0b qword via_acl - io_registers_write
	io_w_9f0c qword io_w_unsupported - io_registers_write
	io_w_9f0d qword via_ifr - io_registers_write
	io_w_9f0e qword via_ier - io_registers_write
	io_w_9f0f qword via_pra - io_registers_write

	; Unused
	io_w_9f10 qword io_w_unsupported - io_registers_write
	io_w_9f11 qword io_w_unsupported - io_registers_write
	io_w_9f12 qword io_w_unsupported - io_registers_write
	io_w_9f13 qword io_w_unsupported - io_registers_write
	io_w_9f14 qword io_w_unsupported - io_registers_write
	io_w_9f15 qword io_w_unsupported - io_registers_write
	io_w_9f16 qword io_w_unsupported - io_registers_write
	io_w_9f17 qword io_w_unsupported - io_registers_write
	io_w_9f18 qword io_w_unsupported - io_registers_write
	io_w_9f19 qword io_w_unsupported - io_registers_write
	io_w_9f1a qword io_w_unsupported - io_registers_write
	io_w_9f1b qword io_w_unsupported - io_registers_write
	io_w_9f1c qword io_w_unsupported - io_registers_write
	io_w_9f1d qword io_w_unsupported - io_registers_write
	io_w_9f1e qword io_w_unsupported - io_registers_write
	io_w_9f1f qword io_w_unsupported - io_registers_write

	vera_w_9f20 qword vera_update_addrl - io_registers_write
	vera_w_9f21 qword vera_update_addrm - io_registers_write
	vera_w_9f22 qword vera_update_addrh - io_registers_write
	vera_w_9f23 qword vera_update_data - io_registers_write
	vera_w_9f24 qword vera_update_data - io_registers_write
	vera_w_9f25 qword vera_update_ctrl - io_registers_write
	vera_w_9f26 qword vera_update_ien - io_registers_write
	vera_w_9f27 qword vera_update_isr - io_registers_write
	vera_w_9f28 qword vera_update_irqline_l - io_registers_write
	vera_w_9f29 qword vera_update_9f29 - io_registers_write
	vera_w_9f2a qword vera_update_9f2a - io_registers_write
	vera_w_9f2b qword vera_update_9f2b - io_registers_write
	vera_w_9f2c qword vera_update_9f2c - io_registers_write
	vera_w_9f2d qword vera_update_l0config - io_registers_write
	vera_w_9f2e qword vera_update_l0mapbase - io_registers_write
	vera_w_9f2f qword vera_update_l0tilebase - io_registers_write
	vera_w_9f30 qword vera_update_l0hscroll_l - io_registers_write
	vera_w_9f31 qword vera_update_l0hscroll_h - io_registers_write
	vera_w_9f32 qword vera_update_l0vscroll_l - io_registers_write
	vera_w_9f33 qword vera_update_l0vscroll_h - io_registers_write
	vera_w_9f34 qword vera_update_l1config - io_registers_write
	vera_w_9f35 qword vera_update_l1mapbase - io_registers_write
	vera_w_9f36 qword vera_update_l1tilebase - io_registers_write
	vera_w_9f37 qword vera_update_l1hscroll_l - io_registers_write
	vera_w_9f38 qword vera_update_l1hscroll_h - io_registers_write
	vera_w_9f39 qword vera_update_l1vscroll_l - io_registers_write
	vera_w_9f3a qword vera_update_l1vscroll_h - io_registers_write
	vera_w_9f3b qword vera_update_audioctrl - io_registers_write
	vera_w_9f3c qword vera_update_audiorate - io_registers_write
	vera_w_9f3d qword vera_update_audiodata - io_registers_write
	vera_w_9f3e qword vera_update_spi_data - io_registers_write
	vera_w_9f3f qword vera_update_spi_ctrl - io_registers_write
	
	ym_w_9f40 qword io_cantwrite - io_registers_write
	ym_w_9f41 qword io_cantwrite_9f41 - io_registers_write
	io_w_9f42 qword io_w_unsupported - io_registers_write
	io_w_9f43 qword io_w_unsupported - io_registers_write
	io_w_9f44 qword io_w_unsupported - io_registers_write
	io_w_9f45 qword io_w_unsupported - io_registers_write
	io_w_9f46 qword io_w_unsupported - io_registers_write
	io_w_9f47 qword io_w_unsupported - io_registers_write
	io_w_9f48 qword io_w_unsupported - io_registers_write
	io_w_9f49 qword io_w_unsupported - io_registers_write
	io_w_9f4a qword io_w_unsupported - io_registers_write
	io_w_9f4b qword io_w_unsupported - io_registers_write
	io_w_9f4c qword io_w_unsupported - io_registers_write
	io_w_9f4d qword io_w_unsupported - io_registers_write
	io_w_9f4e qword io_w_unsupported - io_registers_write
	io_w_9f4f qword io_w_unsupported - io_registers_write
	io_w_9f50 qword io_w_unsupported - io_registers_write
	io_w_9f51 qword io_w_unsupported - io_registers_write
	io_w_9f52 qword io_w_unsupported - io_registers_write
	io_w_9f53 qword io_w_unsupported - io_registers_write
	io_w_9f54 qword io_w_unsupported - io_registers_write
	io_w_9f55 qword io_w_unsupported - io_registers_write
	io_w_9f56 qword io_w_unsupported - io_registers_write
	io_w_9f57 qword io_w_unsupported - io_registers_write
	io_w_9f58 qword io_w_unsupported - io_registers_write
	io_w_9f59 qword io_w_unsupported - io_registers_write
	io_w_9f5a qword io_w_unsupported - io_registers_write
	io_w_9f5b qword io_w_unsupported - io_registers_write
	io_w_9f5c qword io_w_unsupported - io_registers_write
	io_w_9f5d qword io_w_unsupported - io_registers_write
	io_w_9f5e qword io_w_unsupported - io_registers_write
	io_w_9f5f qword io_w_unsupported - io_registers_write
	io_w_9f60 qword io_w_unsupported - io_registers_write
	io_w_9f61 qword io_w_unsupported - io_registers_write
	io_w_9f62 qword io_w_unsupported - io_registers_write
	io_w_9f63 qword io_w_unsupported - io_registers_write
	io_w_9f64 qword io_w_unsupported - io_registers_write
	io_w_9f65 qword io_w_unsupported - io_registers_write
	io_w_9f66 qword io_w_unsupported - io_registers_write
	io_w_9f67 qword io_w_unsupported - io_registers_write
	io_w_9f68 qword io_w_unsupported - io_registers_write
	io_w_9f69 qword io_w_unsupported - io_registers_write
	io_w_9f6a qword io_w_unsupported - io_registers_write
	io_w_9f6b qword io_w_unsupported - io_registers_write
	io_w_9f6c qword io_w_unsupported - io_registers_write
	io_w_9f6d qword io_w_unsupported - io_registers_write
	io_w_9f6e qword io_w_unsupported - io_registers_write
	io_w_9f6f qword io_w_unsupported - io_registers_write
	io_w_9f70 qword io_w_unsupported - io_registers_write
	io_w_9f71 qword io_w_unsupported - io_registers_write
	io_w_9f72 qword io_w_unsupported - io_registers_write
	io_w_9f73 qword io_w_unsupported - io_registers_write
	io_w_9f74 qword io_w_unsupported - io_registers_write
	io_w_9f75 qword io_w_unsupported - io_registers_write
	io_w_9f76 qword io_w_unsupported - io_registers_write
	io_w_9f77 qword io_w_unsupported - io_registers_write
	io_w_9f78 qword io_w_unsupported - io_registers_write
	io_w_9f79 qword io_w_unsupported - io_registers_write
	io_w_9f7a qword io_w_unsupported - io_registers_write
	io_w_9f7b qword io_w_unsupported - io_registers_write
	io_w_9f7c qword io_w_unsupported - io_registers_write
	io_w_9f7d qword io_w_unsupported - io_registers_write
	io_w_9f7e qword io_w_unsupported - io_registers_write
	io_w_9f7f qword io_w_unsupported - io_registers_write
	io_w_9f80 qword io_w_unsupported - io_registers_write
	io_w_9f81 qword io_w_unsupported - io_registers_write
	io_w_9f82 qword io_w_unsupported - io_registers_write
	io_w_9f83 qword io_w_unsupported - io_registers_write
	io_w_9f84 qword io_w_unsupported - io_registers_write
	io_w_9f85 qword io_w_unsupported - io_registers_write
	io_w_9f86 qword io_w_unsupported - io_registers_write
	io_w_9f87 qword io_w_unsupported - io_registers_write
	io_w_9f88 qword io_w_unsupported - io_registers_write
	io_w_9f89 qword io_w_unsupported - io_registers_write
	io_w_9f8a qword io_w_unsupported - io_registers_write
	io_w_9f8b qword io_w_unsupported - io_registers_write
	io_w_9f8c qword io_w_unsupported - io_registers_write
	io_w_9f8d qword io_w_unsupported - io_registers_write
	io_w_9f8e qword io_w_unsupported - io_registers_write
	io_w_9f8f qword io_w_unsupported - io_registers_write
	io_w_9f90 qword io_w_unsupported - io_registers_write
	io_w_9f91 qword io_w_unsupported - io_registers_write
	io_w_9f92 qword io_w_unsupported - io_registers_write
	io_w_9f93 qword io_w_unsupported - io_registers_write
	io_w_9f94 qword io_w_unsupported - io_registers_write
	io_w_9f95 qword io_w_unsupported - io_registers_write
	io_w_9f96 qword io_w_unsupported - io_registers_write
	io_w_9f97 qword io_w_unsupported - io_registers_write
	io_w_9f98 qword io_w_unsupported - io_registers_write
	io_w_9f99 qword io_w_unsupported - io_registers_write
	io_w_9f9a qword io_w_unsupported - io_registers_write
	io_w_9f9b qword io_w_unsupported - io_registers_write
	io_w_9f9c qword io_w_unsupported - io_registers_write
	io_w_9f9d qword io_w_unsupported - io_registers_write
	io_w_9f9e qword io_w_unsupported - io_registers_write
	io_w_9f9f qword io_w_unsupported - io_registers_write
	io_w_9fa0 qword io_w_unsupported - io_registers_write
	io_w_9fa1 qword io_w_unsupported - io_registers_write
	io_w_9fa2 qword io_w_unsupported - io_registers_write
	io_w_9fa3 qword io_w_unsupported - io_registers_write
	io_w_9fa4 qword io_w_unsupported - io_registers_write
	io_w_9fa5 qword io_w_unsupported - io_registers_write
	io_w_9fa6 qword io_w_unsupported - io_registers_write
	io_w_9fa7 qword io_w_unsupported - io_registers_write
	io_w_9fa8 qword io_w_unsupported - io_registers_write
	io_w_9fa9 qword io_w_unsupported - io_registers_write
	io_w_9faa qword io_w_unsupported - io_registers_write
	io_w_9fab qword io_w_unsupported - io_registers_write
	io_w_9fac qword io_w_unsupported - io_registers_write
	io_w_9fad qword io_w_unsupported - io_registers_write
	io_w_9fae qword io_w_unsupported - io_registers_write
	io_w_9faf qword io_w_unsupported - io_registers_write
	io_w_9fb0 qword io_w_unsupported - io_registers_write
	io_w_9fb1 qword io_w_unsupported - io_registers_write
	io_w_9fb2 qword io_w_unsupported - io_registers_write
	io_w_9fb3 qword io_w_unsupported - io_registers_write
	io_w_9fb4 qword io_w_unsupported - io_registers_write
	io_w_9fb5 qword io_w_unsupported - io_registers_write
	io_w_9fb6 qword io_w_unsupported - io_registers_write
	io_w_9fb7 qword io_w_unsupported - io_registers_write
	io_w_9fb8 qword io_w_unsupported - io_registers_write
	io_w_9fb9 qword io_w_unsupported - io_registers_write
	io_w_9fba qword io_w_unsupported - io_registers_write
	io_w_9fbb qword io_w_unsupported - io_registers_write
	io_w_9fbc qword io_w_unsupported - io_registers_write
	io_w_9fbd qword io_w_unsupported - io_registers_write
	io_w_9fbe qword io_w_unsupported - io_registers_write
	io_w_9fbf qword io_w_unsupported - io_registers_write
	io_w_9fc0 qword io_w_unsupported - io_registers_write
	io_w_9fc1 qword io_w_unsupported - io_registers_write
	io_w_9fc2 qword io_w_unsupported - io_registers_write
	io_w_9fc3 qword io_w_unsupported - io_registers_write
	io_w_9fc4 qword io_w_unsupported - io_registers_write
	io_w_9fc5 qword io_w_unsupported - io_registers_write
	io_w_9fc6 qword io_w_unsupported - io_registers_write
	io_w_9fc7 qword io_w_unsupported - io_registers_write
	io_w_9fc8 qword io_w_unsupported - io_registers_write
	io_w_9fc9 qword io_w_unsupported - io_registers_write
	io_w_9fca qword io_w_unsupported - io_registers_write
	io_w_9fcb qword io_w_unsupported - io_registers_write
	io_w_9fcc qword io_w_unsupported - io_registers_write
	io_w_9fcd qword io_w_unsupported - io_registers_write
	io_w_9fce qword io_w_unsupported - io_registers_write
	io_w_9fcf qword io_w_unsupported - io_registers_write
	io_w_9fd0 qword io_w_unsupported - io_registers_write
	io_w_9fd1 qword io_w_unsupported - io_registers_write
	io_w_9fd2 qword io_w_unsupported - io_registers_write
	io_w_9fd3 qword io_w_unsupported - io_registers_write
	io_w_9fd4 qword io_w_unsupported - io_registers_write
	io_w_9fd5 qword io_w_unsupported - io_registers_write
	io_w_9fd6 qword io_w_unsupported - io_registers_write
	io_w_9fd7 qword io_w_unsupported - io_registers_write
	io_w_9fd8 qword io_w_unsupported - io_registers_write
	io_w_9fd9 qword io_w_unsupported - io_registers_write
	io_w_9fda qword io_w_unsupported - io_registers_write
	io_w_9fdb qword io_w_unsupported - io_registers_write
	io_w_9fdc qword io_w_unsupported - io_registers_write
	io_w_9fdd qword io_w_unsupported - io_registers_write
	io_w_9fde qword io_w_unsupported - io_registers_write
	io_w_9fdf qword io_w_unsupported - io_registers_write
	io_w_9fe0 qword io_w_unsupported - io_registers_write
	io_w_9fe1 qword io_w_unsupported - io_registers_write
	io_w_9fe2 qword io_w_unsupported - io_registers_write
	io_w_9fe3 qword io_w_unsupported - io_registers_write
	io_w_9fe4 qword io_w_unsupported - io_registers_write
	io_w_9fe5 qword io_w_unsupported - io_registers_write
	io_w_9fe6 qword io_w_unsupported - io_registers_write
	io_w_9fe7 qword io_w_unsupported - io_registers_write
	io_w_9fe8 qword io_w_unsupported - io_registers_write
	io_w_9fe9 qword io_w_unsupported - io_registers_write
	io_w_9fea qword io_w_unsupported - io_registers_write
	io_w_9feb qword io_w_unsupported - io_registers_write
	io_w_9fec qword io_w_unsupported - io_registers_write
	io_w_9fed qword io_w_unsupported - io_registers_write
	io_w_9fee qword io_w_unsupported - io_registers_write
	io_w_9fef qword io_w_unsupported - io_registers_write
	io_w_9ff0 qword io_w_unsupported - io_registers_write
	io_w_9ff1 qword io_w_unsupported - io_registers_write
	io_w_9ff2 qword io_w_unsupported - io_registers_write
	io_w_9ff3 qword io_w_unsupported - io_registers_write
	io_w_9ff4 qword io_w_unsupported - io_registers_write
	io_w_9ff5 qword io_w_unsupported - io_registers_write
	io_w_9ff6 qword io_w_unsupported - io_registers_write
	io_w_9ff7 qword io_w_unsupported - io_registers_write
	io_w_9ff8 qword io_w_unsupported - io_registers_write
	io_w_9ff9 qword io_w_unsupported - io_registers_write
	io_w_9ffa qword io_w_unsupported - io_registers_write
	io_w_9ffb qword io_w_unsupported - io_registers_write
	io_w_9ffc qword io_w_unsupported - io_registers_write
	io_w_9ffd qword io_w_unsupported - io_registers_write
	io_w_9ffe qword io_w_unsupported - io_registers_write
	io_w_9fff qword io_w_unsupported - io_registers_write
.code