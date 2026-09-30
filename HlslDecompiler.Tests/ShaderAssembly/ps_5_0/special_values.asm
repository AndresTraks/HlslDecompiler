ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps_siv linear noperspective v0.x, position
dcl_output o0
dcl_temps 1
mul r0.x, v0.x, l(1000000015047466219876688855040.0)
and r0.x, r0.x, l(2147483647)
ieq r0.x, r0.x, l(2139095040)
and o0.x, r0.x, l(1065353216)
mov o0.yzw, l(0, 0, 0, 1)
ret
