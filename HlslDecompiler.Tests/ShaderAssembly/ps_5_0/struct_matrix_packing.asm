ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[21], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 2
add r0.xyz, cb0[5].xyz, cb0[8].xyz
add r1.xyz, cb0[11].xyz, cb0[13].xyz
add r0.xyz, r0.xyz, r1.xyz
add r1, cb0[0], cb0[3]
add r1.xyz, r0.xyz, r1.xyz
add r0.xy, cb0[19].xy, cb0[20].xy
mov r0.zw, l(0, 0, 0, 0)
add r0, r0, r1
add o0, r0, v0
ret
