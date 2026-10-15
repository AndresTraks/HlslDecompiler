ps_5_0
dcl_globalFlags refactoringAllowed | enableMinimumPrecision
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input_ps linear v0
dcl_input_ps constant v1.x
dcl_output o0
dcl_temps 3
ilt r0.x, l(2), v1.x
mul r1, v0, cb0[0]
mul r2, r1, l(0.5, 0.5, 0.5, 0.5)
movc o0, r0.x, r1, r2
ret
