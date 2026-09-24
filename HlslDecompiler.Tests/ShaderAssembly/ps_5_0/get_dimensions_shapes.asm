ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_texture2darray (float,float,float,float) t0
dcl_resource_texture3d (float,float,float,float) t1
dcl_output o0
dcl_temps 2
resinfo_uint_indexable(texture2darray)(float,float,float,float) r0.xyz, l(0), t0.xyz
utof r0.xyz, r0.xyz
resinfo_uint_indexable(texture3d)(float,float,float,float) r1, cb0[0].x, t1
utof r1, r1
mov r0.w, l(0)
add o0, r0, r1
ret
