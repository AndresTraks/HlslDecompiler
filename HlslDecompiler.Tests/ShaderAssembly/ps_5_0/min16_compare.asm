ps_5_0
dcl_globalFlags refactoringAllowed | enableMinimumPrecision
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 2
mul r0, v0, cb0[0]
lt r1.x, r0.y, r0.x
movc r0, r1.x, r0, r0.yzwx
mov o0, r0
ret
