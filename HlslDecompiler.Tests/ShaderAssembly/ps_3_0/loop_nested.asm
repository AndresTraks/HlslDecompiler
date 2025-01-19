ps_3_0
def c2, 0, 0, 3, 1
def c3, 5, 0, 0, 0
defi i0, 255, 0, 0, 0
dcl_texcoord v0
mov r0, c2.y
mov r1, c2.y
mov r2.x, c2.z
rep i0
if_ge r2.x, c0.x
break_ne c2.w, -c2.w
endif
mov r3, r1
mov r2.y, c3.x
rep i0
if_ge r2.y, c1.x
break_ne c2.w, -c2.w
endif
add r3, r3, v0
add r2.y, r2.y, c2.w
endrep
mov r1, r3
add r0, r0, v0
add r2.x, r2.x, c2.w
endrep
add oC0, r0, r1
