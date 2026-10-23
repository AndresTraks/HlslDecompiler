vs_3_0
def c6, 0, 0.5, 2, 1
dcl_position v0
dcl_2d s0
dcl_position o0
dcl_texcoord o1.xy
dcl_texcoord1 o2.x
mul r0.xy, c4.zz, v0.xy
mov r0.zw, c6.xx
texldl r1, r0, s0
mul r1.y, r1.x, c4.w
if b0
mov r1.xz, v0.xy
add r1.xzw, r1.xyz, -c5.xyz
dp3 r0.z, r1.xzw, r1.xzw
rsq r0.z, r0.z
rcp r0.z, r0.z
add r0.z, r0.z, -c4.x
rcp r0.w, c4.y
mul_sat r0.z, r0.w, r0.z
mul r1.xz, c6.yy, v0.xy
frc r2.xy, r1.xz
add r1.xz, r1.xz, -r2.xy
mad r1.xz, r1.xz, c6.zz, -v0.xy
mad r2.xy, r0.zz, r1.xz, v0.xy
else
mov r2.xy, v0.xy
endif
mad r2.zw, r1.yy, c6.wx, c6.xw
dp4 o0.x, r2.xzyw, c0
dp4 o0.y, r2.xzyw, c1
dp4 o0.z, r2.xzyw, c2
dp4 o0.w, r2.xzyw, c3
mov o1.xy, r0.xy
mov o2.x, r1.y
