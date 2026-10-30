ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear noperspective v1.xyz
dcl_output o0
dcl_temps 1
mov o0.zw, l(0, 0, 0, 1)
eval_snapped r0.xyz, v1.xyz, l(3, -2, 0, 0)
add o0.x, r0.y, l(1)
add o0.y, r0.x, r0.x
ret
