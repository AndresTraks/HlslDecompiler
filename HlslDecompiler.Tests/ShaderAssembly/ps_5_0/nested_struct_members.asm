ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[3], immediateIndexed
dcl_output o0
dcl_temps 1
mov o0.w, cb0[1].w
add r0.xyz, cb0[0].xyz, cb0[1].xyz
add o0.xy, r0.xy, cb0[2].xy
mov o0.z, r0.z
ret
