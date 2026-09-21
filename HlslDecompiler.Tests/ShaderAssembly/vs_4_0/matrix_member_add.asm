vs_4_0
dcl_constantbuffer CB0[5], immediateIndexed
dcl_input v0
dcl_output o0
dcl_temps 1
mul r0, v0.y, cb0[1]
mad r0, v0.x, cb0[0], r0
mad r0, v0.z, cb0[2], r0
mad r0, v0.w, cb0[3], r0
add o0, r0, cb0[4]
ret
