ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v0.xy
dcl_input_ps_sgv constant v1.x, is_front_face
dcl_input_ps_sgv constant v1.y, sampleIndex
dcl_output o0.xy
dcl_temps 1
ushr r0.w, v0.y, l(1)
mov r0.x, v0.x
mov r0.yz, v1.yy
movc o0.xy, v1.xx, r0.xy, r0.zw
ret
