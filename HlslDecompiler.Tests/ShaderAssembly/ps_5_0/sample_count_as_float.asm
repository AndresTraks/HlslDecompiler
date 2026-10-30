ps_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_texture2dms(0) (float,float,float,float) t0
dcl_input_ps_siv linear noperspective v0.xy, position
dcl_input_ps_sgv constant v1.x, sampleIndex
dcl_output o0
dcl_temps 3
ftoi r0.xy, v0.xy
mov r0.zw, l(0, 0, 0, 0)
ldms_indexable(texture2dms)(float,float,float,float) r0, r0, t0, v1.x
resinfo_uint_indexable(texture2dms)(float,float,float,float) r1.x, l(0), t0.x
utof r1.x, r1.x
mul r1.w, r1.x, l(0.0000999999975)
sampleinfo r1.z, t0.x
samplepos r1.xy, t0.xy, v1.xx
add r2.x, r1.x, l(0.5)
mad o0, r0, r2.x, r1
ret
