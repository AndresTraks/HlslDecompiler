cs_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_typed_texture2d (float,float,float,float) u0
dcl_input vThreadID.xy
dcl_temps 1
dcl_thread_group 8, 8, 1
ld_uav_typed_indexable(texture2d)(float,float,float,float) r0, vThreadID.xyyy, u0
mad r0, r0, l(0.5, 0.5, 0.5, 0.5), l(0.25, 0.25, 0.25, 0.25)
store_uav_typed u0, vThreadID.xyyy, r0
ret
