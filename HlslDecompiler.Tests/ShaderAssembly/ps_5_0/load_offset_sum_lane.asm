ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_texture2darray (float,float,float,float) t0
dcl_input_ps_siv linear noperspective v0.xy, position
dcl_output o0
dcl_temps 3
iadd r0.z, cb0[0].x, l(1)
mov r0.w, l(1)
ftoi r1.xy, v0.xy
iadd r0.xy, r1.xy, cb0[0].yz
ld_indexable(texture2darray)(float,float,float,float) r0, r0, t0
mov r1.z, cb0[0].x
mov r1.w, l(0)
ld_aoffimmi_indexable(1,-1,0)(texture2darray)(float,float,float,float) r2, r1, t0
ld_indexable(texture2darray)(float,float,float,float) r1, r1, t0
mad r0, r0, l(0.5, 0.5, 0.5, 0.5), r2
add o0, -r1, r0
ret
