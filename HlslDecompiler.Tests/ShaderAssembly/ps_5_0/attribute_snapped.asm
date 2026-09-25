ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input_ps linear v1.xy
dcl_input_ps linear v2.xyz
dcl_output o0
dcl_temps 1
eval_centroid r0.xyz, v2.xyz
dp3 o0.z, r0.xyz, cb0[0].zzz
eval_snapped o0.xy, v1.xy, cb0[0].xy
mov o0.w, l(1)
ret
