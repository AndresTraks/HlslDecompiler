vs_3_0
def c87, 4, 0, 0.00999999978, 0
dcl_position v0
dcl_normal v1
dcl_texcoord v2
dcl_blendweight v3
dcl_blendindices v4
dcl_2d s0
dcl_position o0
dcl_texcoord o1.xy
dcl_texcoord1 o2.xyz
dcl_fog o3.x
mov r0.xy, c85.xy
mad r0.xy, v2.xy, r0.xy, c86.xx
mov r0.zw, c87.yy
texldl r0, r0, s0
mul r1, c87.x, v4
mova a0, r1.yxzw
dp4 r2.x, v0, c0[a0.x]
dp4 r2.y, v0, c1[a0.x]
dp4 r2.z, v0, c2[a0.x]
dp4 r2.w, v0, c3[a0.x]
mul r2, r2, v3.y
dp4 r3.x, v0, c0[a0.y]
dp4 r3.y, v0, c1[a0.y]
dp4 r3.z, v0, c2[a0.y]
dp4 r3.w, v0, c3[a0.y]
mad r2, r3, v3.x, r2
dp4 r1.x, v0, c0[a0.z]
dp4 r1.y, v0, c1[a0.z]
dp4 r1.z, v0, c2[a0.z]
dp4 r1.w, v0, c3[a0.z]
mad r1, r1, v3.z, r2
dp4 r2.x, v0, c0[a0.w]
dp4 r2.y, v0, c1[a0.w]
dp4 r2.z, v0, c2[a0.w]
dp4 r2.w, v0, c3[a0.w]
mad r1, r2, v3.w, r1
mad r1.y, r0.x, c84.x, r1.y
dp4 o0.x, r1, c80
dp4 o0.y, r1, c81
dp4 o0.z, r1, c82
dp4 r0.x, r1, c83
dp3 r1.x, v1.xyz, c0[a0.x].xyz
dp3 r1.y, v1.xyz, c1[a0.x].xyz
dp3 r1.z, v1.xyz, c2[a0.x].xyz
mul r0.yzw, r1.xyz, v3.yyy
dp3 r1.x, v1.xyz, c0[a0.y].xyz
dp3 r1.y, v1.xyz, c1[a0.y].xyz
dp3 r1.z, v1.xyz, c2[a0.y].xyz
mad r0.yzw, r1.xyz, v3.xxx, r0.yzw
dp3 r1.x, v1.xyz, c0[a0.z].xyz
dp3 r1.y, v1.xyz, c1[a0.z].xyz
dp3 r1.z, v1.xyz, c2[a0.z].xyz
mad r0.yzw, r1.xyz, v3.zzz, r0.yzw
dp3 r1.x, v1.xyz, c0[a0.w].xyz
dp3 r1.y, v1.xyz, c1[a0.w].xyz
dp3 r1.z, v1.xyz, c2[a0.w].xyz
mad r0.yzw, r1.xyz, v3.www, r0.yzw
dp3 r1.x, r0.yzw, r0.yzw
rsq r1.x, r1.x
mul o2.xyz, r0.yzw, r1.xxx
mul_sat o3.x, r0.x, c87.z
mov o0.w, r0.x
mov o1.xy, v2.xy
