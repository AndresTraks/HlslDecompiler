ps_4_0
dcl_constantbuffer cb0[5], immediateIndexed
dcl_input_ps linear v0.xyz
dcl_output o0
dcl_temps 2
dp3_sat r0.x, v0.xyz, -cb0[2].xyz
dp3_sat r0.y, v0.xyz, -cb0[0].xyz
mad r1.xyz, cb0[1].xyz, r0.yyy, cb0[4].xyz
mad r1.w, cb0[1].w, r0.y, l(1)
mad o0, cb0[3], r0.x, r1
ret
