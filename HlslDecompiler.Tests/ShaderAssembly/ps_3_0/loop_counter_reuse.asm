ps_3_0
def c1, 3, 0, -2, 1
defi i0, 3, 0, 0, 0
dcl_texcoord v0
add r0.x, -c0.x, c0.y
rcp r0.x, r0.x
rcp r0.y, c0.w
mov r1, c1.y
mov r0.z, c1.y
rep i0
mad r2, v0, r0.z, -c0.x
mul_sat r2, r0.x, r2
mad r3, r2, c1.z, c1.x
mul r2, r2, r2
mul r2, r2, r3
add r3, -r0.z, v0
cmp r4, -r3, c1.y, c1.w
cmp r3, r3, -c1.y, -c1.w
add r3, r3, r4
mad r2, r2, r3, r1
mul r0.w, r0.y, r2.x
frc r3.x, r0.w_abs
cmp r0.w, r0.w, r3.x, -r3.x
mad r0.w, r0.w, -c0.w, c0.z
max r3, r2, -c0
min r4, c0, r3
cmp r1, r0.w, r2, r4
add r0.z, r0.z, c1.w
endrep
mov oC0, r1
