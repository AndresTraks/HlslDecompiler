ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps_siv linear noperspective v0.xy, position
dcl_output o0
dcl_temps 1
dp2 r0.x, v0.xy, v0.xy
add r0.x, r0.x, l(1)
sqrt r0.x, r0.x
div r0.yz, v0.xy, r0.xx
div o0.z, l(0.899999976), r0.x
mad r0.x, -r0.z, r0.z, l(1)
mad r0.x, r0.x, l(-0.809999943), l(1)
sqrt r0.x, r0.x
mad r0.x, r0.z, l(0.899999976), r0.x
mad o0.y, r0.z, l(0.899999976), -r0.x
mul r0.x, r0.y, l(0.899999976)
mov o0.x, r0.x
mov o0.w, l(1)
ret
