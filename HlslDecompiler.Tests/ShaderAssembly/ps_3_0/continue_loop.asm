ps_3_0
def c1, 0, 0.5, 0, 0
dcl_color v0.x
mov r0, c1.x
rep i0
if_lt c1.y, v0.x
else
mov r1, r0
rep i0
add r1, r1, c0
endrep
mov r0, r1
endif
endrep
mov oC0, r0
