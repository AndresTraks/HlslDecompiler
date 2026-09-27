ps_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps_siv linear noperspective v0.xy, position
dcl_output o0
dcl_temps 1
add r0.xy, v0.xy, v0.xy
ftoi r0.xy, r0.xy
mov r0.zw, l(0, 0, 0, 0)
ld_indexable(texture2d)(float,float,float,float) r0.x, r0.x, t0.z
mov o0.y, r0.x
ftoi r0.xy, v0.xy
mov r0.zw, l(0, 0, 0, 0)
ld_indexable(texture2d)(float,float,float,float) r0.x, r0.x, t0.y
mov o0.x, r0.x
mov o0.zw, l(0, 0, 0, 1)
ret
