vs_3_0
def c5, 0.159154937, 0.0795774683, 0.5, 1
def c6, 6.28318548, -3.14159274, 0, 0
dcl_position v0
dcl_texcoord v1
dcl_texcoord1 v2
dcl_position o0
dcl_texcoord o1.xy
dcl_fog o2.x
add r0.x, c4.w, v2.x
mad r0.xy, r0.xx, c5.xy, c5.zz
frc r0.xy, r0.xy
mad r0.xy, r0.xy, c6.xx, c6.yy
sincos r1.y, r0.x
sincos r2.x, r0.y
mul r0.x, r1.y, r2.x
mul r0.x, r0.x, c4.z
mul r0.x, r0.x, v1.y
mov r1.xz, v0.xz
add r0.yz, r1.xz, v2.yz
mad r0.xz, c4.xy, r0.xx, r0.yz
mov r0.yw, v0.yw
dp4 o0.x, r0, c0
dp4 o0.y, r0, c1
dp4 o0.z, r0, c2
dp4 o0.w, r0, c3
dp3 r0.x, r0.xyz, r0.xyz
rsq r0.x, r0.x
rcp r0.x, r0.x
mad_sat o2.x, r0.x, -v2.w, c5.w
mov o1.xy, v1.xy
