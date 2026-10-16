ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v0
dcl_input_ps constant v1.x
dcl_output o0
dcl_temps 1
if_z v1.x
mov o0.yzw, v0.yzw
mov r0.x, v0.x
else
mov o0.yzw, -v0.yzw
mov r0.x, -v0.x
endif
add o0.x, r0.x, v0.y
ret
