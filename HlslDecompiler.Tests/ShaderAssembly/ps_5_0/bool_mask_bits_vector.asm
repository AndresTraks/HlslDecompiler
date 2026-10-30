ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[2], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 2
lt r0, cb0[0], v0
lt r1.xyz, v0.xyz, cb0[1].xyz
mov r1.w, l(-1)
ine r0, r0, r1
and r0, r0, l(1065353216, 1065353216, 1065353216, 1065353216)
add o0, r0, v0
ret
