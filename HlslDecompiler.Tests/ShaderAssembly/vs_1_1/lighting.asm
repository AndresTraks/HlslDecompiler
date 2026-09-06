vs_1_1
dcl_normal v0
dp3 r0.x, v0.xyz, c0.xyz
dp3 r0.y, v0.xyz, c1.xyz
mov r0.zw, c2.xx
lit r0, r0
mad oPos, c3, r0.y, r0.z
