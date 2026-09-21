ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[5], immediateIndexed
dcl_input_ps linear v0.xyz
dcl_output o0
dcl_temps 1
mul r0.xyz, cb0[1].www, cb0[1].xyz
mad r0.xyz, cb0[0].xyz, cb0[0].www, r0.xyz
mad o0.xyz, r0.xyz, v0.xyz, cb0[2].xyz
add o0.w, cb0[3].x, cb0[4].y
ret
