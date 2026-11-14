ps_5_0
dcl_globalFlags refactoringAllowed | enableMinimumPrecision
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input_ps constant v0
dcl_output o0
dcl_temps 1
imad r0, v0, cb0[0], l(3, 3, 3, 3)
ishr r0, r0, l(1, 1, 1, 1)
itof o0, r0
ret
