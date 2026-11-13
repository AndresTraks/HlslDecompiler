vs_2_0
def c124, 765.005859, 1, 0, 0
dcl_position v0
dcl_blendweight v1
dcl_blendindices v2
dcl_normal v3
mul r0.xy, v2.zy, c124.xx
mova a0.xy, r0.yx
mul r0, v1.y, c0[a0.x]
mad r0, c0[a0.y], v1.x, r0
dp4 r1.x, r0, v0
dp3 r0.x, r0.xyz, v3.xyz
mul r2, v1.y, c1[a0.x]
mul r3, v1.y, c2[a0.x]
mad r3, c2[a0.y], v1.x, r3
mad r2, c1[a0.y], v1.x, r2
dp4 r1.y, r2, v0
dp3 r0.y, r2.xyz, v3.xyz
dp4 r1.z, r3, v0
dp3 r0.z, r3.xyz, v3.xyz
mov r1.w, c124.y
dp4 oPos.x, r1, c120
dp4 oPos.y, r1, c121
dp4 oPos.z, r1, c122
dp4 oPos.w, r1, c123
dp3 r0.w, r0.xyz, r0.xyz
rsq r0.w, r0.w
mul oT0.xyz, r0.www, r0.xyz
