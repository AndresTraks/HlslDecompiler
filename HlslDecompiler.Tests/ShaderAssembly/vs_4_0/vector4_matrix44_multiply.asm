vs_4_0
dcl_constantbuffer cb0[4], immediateIndexed
dcl_input v0
dcl_output o0
dcl_output o1
dcl_output o2
dcl_output o3
dcl_temps 1
dp4 o0.x, v0, cb0[0]
dp4 o0.y, v0, cb0[1]
dp4 o0.z, v0, cb0[2]
dp4 o0.w, v0, cb0[3]
dp4 o1.x, v0.yxzw, cb0[0]
dp4 o1.y, v0.yxzw, cb0[1]
dp4 o1.z, v0.yxzw, cb0[2]
dp4 o1.w, v0.yxzw, cb0[3]
dp4 o2.x, |v0.yxzw|, cb0[0]
dp4 o2.y, |v0.yxzw|, cb0[1]
dp4 o2.z, |v0.yxzw|, cb0[2]
dp4 o2.w, |v0.yxzw|, cb0[3]
mul r0, v0, l(5, 2, 3, 4)
dp4 o3.x, r0, cb0[0]
dp4 o3.y, r0, cb0[1]
dp4 o3.z, r0, cb0[2]
dp4 o3.w, r0, cb0[3]
ret
