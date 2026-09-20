ps_4_1
dcl_globalFlags refactoringAllowed
dcl_resource_texture2dms (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 2
sampleinfo_uint r0.x, t0.x
iadd r0.x, r0.x, l(-1)
ftoi r1.xy, v0.xy
mov r1.zw, l(0, 0, 0, 0)
ldms r0, r1, t0, r0.x
resinfo_uint r1.xy, l(0), t0.xy
iadd r1.x, r1.y, r1.x
utof r1.x, r1.x
mul o0, r0, r1.x
ret
