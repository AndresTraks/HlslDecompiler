vs_1_1
dcl_blendindices v0
dcl_blendweight v1
mov a0.x, v0.y
mul r0, v1.y, c0[a0.x]
mov a0.x, v0.x
mad r0, c0[a0.x], v1.x, r0
mov a0.x, v0.z
mad r0, c0[a0.x], v1.z, r0
dp4 oPos.x, r0, c16
dp4 oPos.y, r0, c17
dp4 oPos.z, r0, c18
dp4 oPos.w, r0, c19
