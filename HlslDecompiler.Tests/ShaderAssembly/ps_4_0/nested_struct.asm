ps_4_0
dcl_constantbuffer cb0[2], immediateIndexed
dcl_input_ps linear v0.xyz
dcl_output o0
dcl_temps 1
dp3 r0.x, v0.xyz, cb0[0].xyz
mad o0, cb0[1], r0.x, cb0[0].w
ret
