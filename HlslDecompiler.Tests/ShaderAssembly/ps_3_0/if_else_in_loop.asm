ps_3_0
def c2, 0, 0, 1, 0.5
defi i0, 255, 0, 0, 0
dcl_texcoord v0
mov r0, c2.y
mov r1.x, c2.y
rep i0
if_ge r1.x, c0.x
break_ne c2.z, -c2.z
endif
if_lt c1.x, v0.x
add r0, r0, v0
else
mul r0, r0, c2.w
endif
add r1.x, r1.x, c2.z
endrep
mov oC0, r0
