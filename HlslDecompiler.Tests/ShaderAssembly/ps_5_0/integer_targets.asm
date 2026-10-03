ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps_siv linear noperspective v0.xy, position
dcl_input_ps_sgv constant v1.x, primitive_id
dcl_output o0
dcl_output o1
imad o0.y, v1.x, l(7), l(3)
and o0.z, v1.x, l(255)
mov o0.x, v1.x
mov o0.w, l(1)
utof o1.z, v1.x
mul o1.xy, v0.xy, l(0.00100000005, 0.00100000005, 0, 0)
mov o1.w, l(1)
ret
