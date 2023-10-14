vs_4_0
dcl_constantbuffer cb0[3], immediateIndexed
dcl_input v0.xyz
dcl_output o0.xyzw
dcl_temps 1
mul r0.xyzw, v0.yyyy, cb0[1].xyzw
mad r0.xyzw, cb0[0].xyzw, v0.xxxx, r0.xyzw
mad o0.xyzw, cb0[2].xyzw, v0.zzzz, r0.xyzw
ret
