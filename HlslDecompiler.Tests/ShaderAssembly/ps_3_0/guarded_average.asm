ps_3_0
def c9, -4, -5, -6, -7
def c10, 0, -1, -2, -3
def c11, 0, 1, 0, -0.0000999999975
def c12, 10000, 0, 0, 0
defi i0, 8, 0, 0, 0
dcl_texcoord v0.xy
dcl_2d s0
mov r0.xy, c11.xy
mul r1.zw, r0.xy, c8.yy
mov r2, c11.x
mov r0.yz, c11.xx
rep i0
add r3, r0.z, c10
add r4, r0.z, c9
cmp r0.w, -r3_abs.x, c0.w, r0.x
cmp r0.w, -r3_abs.y, c1.w, r0.w
cmp r0.w, -r3_abs.z, c2.w, r0.w
cmp r0.w, -r3_abs.w, c3.w, r0.w
cmp r0.w, -r4_abs.x, c4.w, r0.w
cmp r0.w, -r4_abs.y, c5.w, r0.w
cmp r0.w, -r4_abs.z, c6.w, r0.w
cmp r0.w, -r4_abs.w, c7.w, r0.w
if_ge -r0.w, c11.x
break_ne c11.y, -c11.y
endif
cmp r5.xy, -r3_abs.xx, c0.xy, r0.xx
cmp r3.xy, -r3_abs.yy, c1.xy, r5.xy
cmp r3.xy, -r3_abs.zz, c2.xy, r3.xy
cmp r3.xy, -r3_abs.ww, c3.xy, r3.xy
cmp r3.xy, -r4_abs.xx, c4.xy, r3.xy
cmp r3.xy, -r4_abs.yy, c5.xy, r3.xy
cmp r3.xy, -r4_abs.zz, c6.xy, r3.xy
cmp r3.xy, -r4_abs.ww, c7.xy, r3.xy
mad r1.xy, r3.xy, c8.xx, v0.xy
texldl r3, r1, s0
mad r2, r3, r0.w, r2
add r0.y, r0.w, r0.y
add r0.z, r0.z, c11.y
endrep
add r0.x, r0.y, c11.w
rcp r0.y, r0.y
cmp r0.x, r0.x, r0.y, c12.x
mul oC0, r0.x, r2
