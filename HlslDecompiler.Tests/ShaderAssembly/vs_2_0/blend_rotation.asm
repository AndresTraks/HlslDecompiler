vs_2_0
def c124, 765.005859, 0, 0, 0
dcl_position v0
dcl_blendweight v1
dcl_blendindices v2
dcl_normal v3
dp4 oPos.x, v0, c120
dp4 oPos.y, v0, c121
dp4 oPos.z, v0, c122
dp4 oPos.w, v0, c123
mul r0.xy, v2.zy, c124.xx
mova a0.xy, r0.xy
mul r0.xy, v1.xx, c1[a0.x].xy
mov r1.w, r0.x
mul r2.xy, v1.yy, c1[a0.y].xy
mov r3.w, r2.x
mul r1.z, v1.x, c2[a0.x].x
mul r3.z, v1.y, c2[a0.y].x
mul r1.xy, v1.xx, c0[a0.x].xy
mul r3.xy, v1.yy, c0[a0.y].xy
add r1.xzw, r1.xwz, r3.xwz
dp3 oT0.x, r1.xzw, v3.xyz
mul r1.xw, v1.xx, c2[a0.x].yz
mov r0.zw, r1.yx
mul r3.xw, v1.yy, c2[a0.y].yz
mov r2.zw, r3.yx
add r0.xyz, r0.zyw, r2.zyw
dp3 oT0.y, r0.xyz, v3.xyz
mul r1.y, v1.x, c0[a0.x].z
mul r1.z, v1.x, c1[a0.x].z
mul r3.y, v1.y, c0[a0.y].z
mul r3.z, v1.y, c1[a0.y].z
add r0.xyz, r1.yzw, r3.yzw
dp3 oT0.z, r0.xyz, v3.xyz
