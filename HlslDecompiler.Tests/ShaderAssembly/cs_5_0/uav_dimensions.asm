cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_texture2d (float,float,float,float) t0
dcl_uav_typed_texture2d (float,float,float,float) u0
dcl_uav_typed_texture3d (float,float,float,float) u1
dcl_input vThreadID.xyz
dcl_temps 2
dcl_thread_group 8, 8, 1
resinfo_uint_indexable(texture2d)(float,float,float,float) r0.xy, l(0), t0.xy
utof r0.xy, r0.xy
resinfo_uint_indexable(texture2d)(float,float,float,float) r1.xy, l(0), u0.xy
utof r0.zw, r1.xy
store_uav_typed u0, vThreadID.xyyy, r0
resinfo_uint_indexable(texture3d)(float,float,float,float) r0.xyz, l(0), u1.xyz
utof r0.xyz, r0.xyz
mov r0.w, l(1)
store_uav_typed u1, vThreadID.xyzz, r0
ret
