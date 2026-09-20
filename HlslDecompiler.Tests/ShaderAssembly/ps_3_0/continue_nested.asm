ps_3_0
def c1, 0, 5, 3, 0
mov r0, c1.x
rep i0
if_lt c1.y, r0.w
else
mov r1, r0.wxyz
rep i0
add r2.x, -r1.y, c1.z
add r3, r1, c0
cmp r1, r2.x, r3, r1
endrep
mov r0, r1.yzwx
endif
endrep
mov oC0.yzw, r0.xyz
mov oC0.x, r0.w
