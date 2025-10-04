vs_4_0
dcl_constantbuffer cb0[4], immediateIndexed
dcl_input v0
dcl_output o0
dcl_output o1
dcl_output o2
dcl_output o3
dcl_temps 2
mul r0, v0.y, cb0[1]
mad r0, cb0[0], v0.x, r0
mad r0, cb0[2], v0.z, r0
mad o0, cb0[3], v0.w, r0
mul r0, v0.x, cb0[1]
mad r0, cb0[0], v0.y, r0
mad r0, cb0[2], v0.z, r0
mad o1, cb0[3], v0.w, r0
mul r0, |v0.x|, cb0[1]
mad r0, cb0[0], |v0.y|, r0
mad r0, cb0[2], |v0.z|, r0
mad o2, cb0[3], |v0.w|, r0
mul r0, v0, l(5, 2, 3, 4)
mul r1, r0.y, cb0[1]
mad r1, cb0[0], r0.x, r1
mad r1, cb0[2], r0.z, r1
mad o3, cb0[3], r0.w, r1
ret
