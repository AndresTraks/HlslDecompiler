ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps_siv linear noperspective v0.x, position
dcl_output o0
dcl_temps 1
mov o0.w, l(1)
round_z r0.x, v0.x
add o0.xy, -r0.xx, v0.xx
mov o0.z, r0.x
ret
