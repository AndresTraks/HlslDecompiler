ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v0.y
dcl_input_ps constant v1.x
dcl_output o0
dcl_temps 1
mov o0.w, l(1)
ieq r0.x, v1.x, l(3)
movc r0.x, r0.x, l(7), v0.y
ushr r0.y, r0.x, l(30)
utof o0.y, r0.y
xor o0.z, r0.x, l(-2147483648)
mov o0.x, r0.x
ret
