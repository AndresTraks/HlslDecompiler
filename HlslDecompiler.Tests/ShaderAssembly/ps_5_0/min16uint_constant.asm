ps_5_0
dcl_globalFlags refactoringAllowed | enableMinimumPrecision
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input_ps constant v0
dcl_output o0
dcl_temps 2
and r0, v0, cb0[0]
ushr r1, v0, l(2, 2, 2, 2)
or r0, r0, r1
utof o0, r0
ret
