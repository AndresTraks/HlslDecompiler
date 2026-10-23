ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v1.x
dcl_input_ps linear v1.yz
dcl_input_ps linear v2.x
dcl_input_ps linear v3.x
dcl_output o0
add o0.x, v1.y, v1.x
mul o0.z, v1.z, v3.x
mov o0.y, v2.x
mov o0.w, l(1)
ret
