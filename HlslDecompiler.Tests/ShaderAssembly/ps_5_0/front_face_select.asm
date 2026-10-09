ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps_siv linear noperspective v0.z, position
dcl_input_ps linear v1.xy
dcl_input_ps_sgv constant v2.x, is_front_face
dcl_output o0
dcl_output oMask
dcl_output oDepth
dcl_temps 1
mov r0.xy, v1.xy
mov r0.zw, l(0, 0, 0, 1)
movc o0, v2.x, r0, r0.zxyw
mad_sat oDepth, v1.x, l(0.00999999978), v0.z
lt r0.x, l(0.5), v1.y
movc oMask, r0.x, l(15), l(3)
ret
