vs_2_0
def c94, 765.005859, 1, 0, 0
dcl_position v0
dcl_normal v1
dcl_blendweight v2
dcl_blendindices v3
mul r0.xy, v3.zy, c94.xx
mova a0.xy, r0.yx
mul r0, v2.y, c0[a0.x]
mad r0, c0[a0.y], v2.x, r0
dp4 r1.x, v0, r0
dp3 r0.x, v1.xyz, r0.xyz
mul r2, v2.y, c1[a0.x]
mul r3, v2.y, c2[a0.x]
mad r3, c2[a0.y], v2.x, r3
mad r2, c1[a0.y], v2.x, r2
dp4 r1.y, v0, r2
dp3 r0.y, v1.xyz, r2.xyz
dp4 r1.z, v0, r3
dp3 r0.z, v1.xyz, r3.xyz
mov r1.w, c94.y
dp4 oPos.x, r1, c90
dp4 oPos.y, r1, c91
dp4 oPos.z, r1, c92
dp4 oPos.w, r1, c93
dp3 r0.w, r0.xyz, r0.xyz
rsq r0.w, r0.w
mul oT0.xyz, r0.www, r0.xyz
