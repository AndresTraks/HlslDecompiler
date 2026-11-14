ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 2
mul_sat r0, v0, cb0[0]
dp3 r1.x, r0.xyz, r0.xyz
rsq r1.x, r1.x
mul o0.xyz, r0.xyz, r1.xxx
dp4 o0.w, r0, cb0[0]
ret
