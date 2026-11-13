ps_3_0
def c8, -4, -5, -6, -7
def c9, 0, -1, -2, -3
def c10, 0, 0, 1, 0
defi i0, 8, 0, 0, 0
dcl_texcoord v0.xy
dcl_2d s0
mov r0.zw, c10.xx
mov r1, c10.x
mov r2.x, c10.x
rep i0
add r3, r2.x, c9
add r4, r2.x, c8
mov r5.x, c10.x
cmp r2.y, -r3_abs.x, c0.z, r5.x
cmp r2.y, -r3_abs.y, c1.z, r2.y
cmp r2.y, -r3_abs.z, c2.z, r2.y
cmp r2.y, -r3_abs.w, c3.z, r2.y
cmp r2.y, -r4_abs.x, c4.z, r2.y
cmp r2.y, -r4_abs.y, c5.z, r2.y
cmp r2.y, -r4_abs.z, c6.z, r2.y
cmp r2.y, -r4_abs.w, c7.z, r2.y
add r2.z, r2.x, c10.z
if_ge r2.y, c10.x
cmp r2.y, r2.y, r2.x, r2.z
add r3, r2.y, c9
add r4, r2.y, c8
cmp r5.xyz, -r3_abs.xxx, c0.xyz, r5.xxx
cmp r5.xyz, -r3_abs.yyy, c1.xyz, r5.xyz
cmp r3.xyz, -r3_abs.zzz, c2.xyz, r5.xyz
cmp r3.xyz, -r3_abs.www, c3.xyz, r3.xyz
cmp r3.xyz, -r4_abs.xxx, c4.xyz, r3.xyz
cmp r3.xyz, -r4_abs.yyy, c5.xyz, r3.xyz
cmp r3.xyz, -r4_abs.zzz, c6.xyz, r3.xyz
cmp r3.xyz, -r4_abs.www, c7.xyz, r3.xyz
add r0.xy, r3.xy, v0.xy
texldl r4, r0, s0
mad r1, r4, r3.z, r1
endif
mov r2.x, r2.z
endrep
mov oC0, r1
