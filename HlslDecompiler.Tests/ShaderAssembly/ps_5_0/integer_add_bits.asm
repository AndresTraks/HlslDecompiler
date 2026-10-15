ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v0
dcl_input_ps constant v1.x
dcl_output o0
dcl_output oDepth
dcl_temps 1
iadd o0, v0, v1.x
lt r0.x, l(0), v0.x
bfi r0.y, l(23), l(0), v1.x, l(1056964608)
movc oDepth, r0.x, r0.y, v0.y
ret
