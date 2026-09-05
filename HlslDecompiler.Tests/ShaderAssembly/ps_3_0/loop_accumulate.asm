ps_3_0
def c1, 0, 0, 1, 0.5
defi i0, 255, 0, 0, 0
dcl_texcoord v0
mov r0, c1.y
mov r1.x, c1.y
rep i0
if_ge r1.x, c0.x
break_ne c1.z, -c1.z
endif
mad r0, r0, c1.w, v0
add r1.x, r1.x, c1.z
endrep
mov oC0, r0
