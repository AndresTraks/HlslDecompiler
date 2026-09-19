ps_4_1
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v0
dcl_output o0
dcl_output oMask
dcl_temps 1
mov o0, v0
lt r0.x, l(0.5), v0.w
movc oMask, r0.x, l(15), l(5)
ret
