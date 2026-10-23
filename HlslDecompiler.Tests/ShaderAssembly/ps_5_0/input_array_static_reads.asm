ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v1
dcl_input_ps linear v2
dcl_input_ps linear v3
dcl_input_ps linear v4
dcl_input_ps constant v5.x
dcl_output o0
dcl_temps 2
dcl_indexrange v1 4
mad r0, v2, l(2, 2, 2, 2), v1
mad r0, v3, l(3, 3, 3, 3), r0
mad r0, v4, l(4, 4, 4, 4), r0
and r1.x, v5.x, l(3)
mul o0, r0, v[r1.x + 1]
ret
