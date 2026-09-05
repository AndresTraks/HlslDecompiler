ps_3_0
def c1, 0, 0, 1, 0
defi i0, 255, 0, 0, 0
dcl_texcoord v0
mov r0, c1.y
mov r1, c1.z
mov r2.x, c1.y
rep i0
if_ge r2.x, c0.x
break_ne c1.z, -c1.z
endif
add r0, r0, v0
mul r1, r1, v0
add r2.x, r2.x, c1.z
endrep
add oC0, r0, r1
