ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps_siv linear noperspective v0.xy, position
dcl_output o0
dcl_temps 1
dp2 r0.x, v0.xy, v0.xy
add r0.x, r0.x, l(1)
sqrt r0.y, r0.x
rsq r0.x, r0.x
mov o0.z, -r0.x
div o0.xy, -v0.xy, r0.yy
mov o0.w, l(1)
ret
