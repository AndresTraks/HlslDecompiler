ps_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_texture2dms(4) (float,float,float,float) t0
dcl_input_ps_siv linear noperspective v0.xy, position
dcl_input_ps_sgv constant v1.x, sampleIndex
dcl_output o0
dcl_temps 2
ftoi r0.xy, v0.xy
mov r0.zw, l(0, 0, 0, 0)
ldms_indexable(texture2dms)(float,float,float,float) r0, r0, t0, v1.x
resinfo_uint_indexable(texture2dms)(float,float,float,float) r1.x, l(0), t0.x
ishl r1.x, r1.x, l(2)
utof r1.x, r1.x
samplepos r1.xy, t0.yx, v1.xx
mad o0, r0, r1.y, r1.x
ret
