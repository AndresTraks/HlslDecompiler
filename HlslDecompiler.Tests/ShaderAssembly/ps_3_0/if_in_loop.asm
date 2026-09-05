ps_3_0
def c2, 0, 0, 1, 0
defi i0, 255, 0, 0, 0
dcl_texcoord v0
add r0.x, c1.x, -v0.x
mov r1, c2.y
mov r0.y, c2.y
rep i0
if_ge r0.y, c0.x
break_ne c2.z, -c2.z
endif
add r2, r1, v0
cmp r1, r0.x, r1, r2
add r0.y, r0.y, c2.z
endrep
mov oC0, r1
