vs_1_1
def c64, 765.005859, 1, 0, 0
dcl_position v0
dcl_blendweight v1
dcl_blendindices v2
add r0.x, -v1.x, c64.y
mul r0.yz, v2.zy, c64.xx
mov a0.x, r0.z
dp4 r1.x, v0, c0[a0.x]
dp4 r1.y, v0, c1[a0.x]
dp4 r1.z, v0, c2[a0.x]
mul r0.xzw, r0.xxx, r1.xyz
mov a0.x, r0.y
dp4 r1.x, v0, c0[a0.x]
dp4 r1.y, v0, c1[a0.x]
dp4 r1.z, v0, c2[a0.x]
mad r0.xyz, r1.xyz, v1.xxx, r0.xzw
mov r0.w, c64.y
dp4 oPos.x, r0, c60
dp4 oPos.y, r0, c61
dp4 oPos.z, r0, c62
dp4 oPos.w, r0, c63
