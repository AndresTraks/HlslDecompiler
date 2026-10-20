ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v1
dcl_output o0
dcl_output oMask
dcl_output oStencilRef
dcl_temps 1
mov o0, v1
and oStencilRef, v1.x, l(255)
lt r0.x, l(0.5), v1.y
movc oMask, r0.x, l(15), v1.z
ret
