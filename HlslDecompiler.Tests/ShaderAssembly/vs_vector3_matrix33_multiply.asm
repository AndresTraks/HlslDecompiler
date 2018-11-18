vs_3_0
def c3, 1, 2, 3, 0
dcl_position v0
dcl_position o0
dcl_position1 o1.xyz
dcl_position2 o2.xyz
dcl_position3 o3.xyz
dp3 o0.x, v0.xyz, c0.xyz
dp3 o0.y, v0.xyz, c1.xyz
dp3 o0.z, v0.xyz, c2.xyz
dp3 o1.x, v0.yxz, c0.xyz
dp3 o1.y, v0.yxz, c1.xyz
dp3 o1.z, v0.yxz, c2.xyz
dp3 o2.x, v0.yxz_abs, c0.xyz
dp3 o2.y, v0.yxz_abs, c1.xyz
dp3 o2.z, v0.yxz_abs, c2.xyz
mul r0.xyz, c3.xyz, v0.yxz
dp3 o3.x, r0.xyz, c0.xyz
dp3 o3.y, r0.xyz, c1.xyz
dp3 o3.z, r0.xyz, c2.xyz
mov o0.w, c3.x
