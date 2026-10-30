ps_3_0
def c2, 0, 1, 0, 0
dcl_texcoord v0.xy
dcl_2d s0
mov r0.zw, c2.xx
mov r1, c2.x
mov r2.x, c2.x
rep i0
mad r0.xy, c0.xy, r2.xx, v0.xy
texldl r3, r0, s0
add r1, r1, r3
add r2.x, r2.x, c2.y
endrep
rcp r0.x, c1.x
mul oC0, r0.x, r1
