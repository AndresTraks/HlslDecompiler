ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v0.xzw
dcl_input_ps constant v1.x
dcl_output o0
dcl_temps 2
ieq r0.x, v1.x, l(2)
if_nz r0.x
mov r0.x, l(1075838976)
else
mov r0.x, v0.x
endif
ult r1.x, l(4), v1.x
movc r0.y, r1.x, l(1065353216), v0.z
mov r0.zw, v0.zw
mov o0, r0
ret
