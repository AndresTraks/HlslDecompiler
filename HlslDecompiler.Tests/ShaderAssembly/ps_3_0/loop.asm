ps_3_0
def c1, 0, 0, 3, 1
defi i0, 255, 0, 0, 0
dcl_texcoord v0
mov r0, c1.y
mov r1.x, c1.z
rep i0
if_lt r1.x, c0.x
break_ne c1.w, -c1.w
endif
add r0, r0, v0
add r1.x, r1.x, c1.w
endrep
mov oC0, r0
