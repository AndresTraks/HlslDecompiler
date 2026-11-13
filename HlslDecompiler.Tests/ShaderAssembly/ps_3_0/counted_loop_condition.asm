ps_3_0
def c1, 0, 0, 0, 0
defi i0, 12, 0, 0, 0
dcl_texcoord v0.xy
dcl_2d s0
mov r0.zw, c1.xx
mov r1, c1.x
mov r2.x, c1.x
rep i0
break_ge r2.x, c0.x
mad r0.xy, r2.xx, c0.zw, v0.xy
texldl r3, r0, s0
add r1, r1, r3
add r2.x, r2.x, c0.y
endrep
mov oC0, r1
