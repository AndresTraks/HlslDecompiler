ps_3_0
def c17, 0, -1, -2, -3
def c18, -4, -5, -6, -7
def c19, -8, -9, -10, -11
def c20, -12, -13, -14, -15
def c21, 0, 0, 1, 0
defi i0, 16, 0, 0, 0
dcl_texcoord v0.xy
dcl_2d s0
mov r0.zw, c21.xx
mov r1, c21.x
mov r2.x, c21.x
rep i0
add r3, r2.x, c17
add r4, r2.x, c18
add r5, r2.x, c19
add r6, r2.x, c20
mov r7.x, c21.x
cmp r2.yz, -r3_abs.xx, c0.xy, r7.xx
cmp r2.yz, -r3_abs.yy, c1.xy, r2.yz
cmp r2.yz, -r3_abs.zz, c2.xy, r2.yz
cmp r2.yz, -r3_abs.ww, c3.xy, r2.yz
cmp r2.yz, -r4_abs.xx, c4.xy, r2.yz
cmp r2.yz, -r4_abs.yy, c5.xy, r2.yz
cmp r2.yz, -r4_abs.zz, c6.xy, r2.yz
cmp r2.yz, -r4_abs.ww, c7.xy, r2.yz
cmp r2.yz, -r5_abs.xx, c8.xy, r2.yz
cmp r2.yz, -r5_abs.yy, c9.xy, r2.yz
cmp r2.yz, -r5_abs.zz, c10.xy, r2.yz
cmp r2.yz, -r5_abs.ww, c11.xy, r2.yz
cmp r2.yz, -r6_abs.xx, c12.xy, r2.yz
cmp r2.yz, -r6_abs.yy, c13.xy, r2.yz
cmp r2.yz, -r6_abs.zz, c14.xy, r2.yz
cmp r2.yz, -r6_abs.ww, c15.xy, r2.yz
add r0.xy, r2.yz, v0.xy
texldl r3, r0, s0
if_lt c16.x, r3.w
break_ne c21.z, -c21.z
endif
add r1, r1, r3
add r2.x, r2.x, c21.z
endrep
mov oC0, r1
