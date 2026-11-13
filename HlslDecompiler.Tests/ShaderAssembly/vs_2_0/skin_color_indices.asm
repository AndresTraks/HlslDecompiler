vs_2_0
def c140, 765.005859, 255.001953, 1, 0
dcl_position v0
dcl_blendweight v1
dcl_blendindices v2
dcl_blendindices1 v3
mul r0, v2.zyxw, c140.x
mova a0.x, r0.y
dp4 r1.x, v0, c0[a0.x]
dp4 r1.y, v0, c1[a0.x]
dp4 r1.z, v0, c2[a0.x]
mul r1.xyz, r1.xyz, v1.yyy
mova a0.x, r0.x
dp4 r2.x, v0, c0[a0.x]
dp4 r2.y, v0, c1[a0.x]
dp4 r2.z, v0, c2[a0.x]
mad r1.xyz, r2.xyz, v1.xxx, r1.xyz
mova a0.xy, r0.zw
dp4 r0.x, v0, c0[a0.x]
dp4 r0.y, v0, c1[a0.x]
dp4 r0.z, v0, c2[a0.x]
mad r0.xyz, r0.xyz, v1.zzz, r1.xyz
dp4 r1.x, v0, c0[a0.y]
dp4 r1.y, v0, c1[a0.y]
dp4 r1.z, v0, c2[a0.y]
mad r0.xyz, r1.xyz, v1.www, r0.xyz
mov r0.w, c140.z
dp4 oPos.x, r0, c136
dp4 oPos.y, r0, c137
dp4 oPos.z, r0, c138
dp4 oPos.w, r0, c139
mul r0.x, v3.y, c140.y
mova a0.x, r0.x
mov oD0, c120[a0.x]
