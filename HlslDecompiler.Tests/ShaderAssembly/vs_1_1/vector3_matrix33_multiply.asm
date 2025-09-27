vs_1_1
def c3, 1, 2, 3, 0
dcl_position v0
dp3 oPos.x, v0.xyz, c0.xyz
dp3 oPos.y, v0.xyz, c1.xyz
dp3 oPos.z, v0.xyz, c2.xyz
dp3 oT1.x, v0.yxz, c0.xyz
dp3 oT1.y, v0.yxz, c1.xyz
dp3 oT1.z, v0.yxz, c2.xyz
max r0.xyz, -v0.yxz, v0.yxz
dp3 oT2.x, r0.xyz, c0.xyz
dp3 oT2.y, r0.xyz, c1.xyz
dp3 oT2.z, r0.xyz, c2.xyz
mul r0.xyz, v0.yxz, c3.xyz
dp3 oT3.x, r0.xyz, c0.xyz
dp3 oT3.y, r0.xyz, c1.xyz
dp3 oT3.z, r0.xyz, c2.xyz
mov oPos.w, c3.x
