vs_4_0
dcl_constantbuffer cb0[3], immediateIndexed
dcl_input v0.xyz
dcl_output o0
dcl_temps 1
mul r0, v0.y, cb0[1]
mad r0, cb0[0], v0.x, r0
mad o0, cb0[2], v0.z, r0
ret
