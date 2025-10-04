vs_4_0
dcl_constantbuffer cb0[3], immediateIndexed
dcl_input v0.xyz
dcl_output o0
dcl_output o1.xyz
dcl_output o2.xyz
dcl_output o3.xyz
dcl_temps 1
dp3 o0.x, v0.xyz, cb0[0].xyz
dp3 o0.y, v0.xyz, cb0[1].xyz
dp3 o0.z, v0.xyz, cb0[2].xyz
mov o0.w, l(1)
dp3 o1.x, v0.yxz, cb0[0].xyz
dp3 o1.y, v0.yxz, cb0[1].xyz
dp3 o1.z, v0.yxz, cb0[2].xyz
dp3 o2.x, |v0.yxz|, cb0[0].xyz
dp3 o2.y, |v0.yxz|, cb0[1].xyz
dp3 o2.z, |v0.yxz|, cb0[2].xyz
mul r0.xyz, v0.yxz, l(1, 2, 3, 0)
dp3 o3.x, r0.xyz, cb0[0].xyz
dp3 o3.y, r0.xyz, cb0[1].xyz
dp3 o3.z, r0.xyz, cb0[2].xyz
ret
