cs_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_typed_buffer (float,float,float,float) u0
dcl_uav_typed_buffer (float,float,float,float) u1
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 8, 1, 1
ld_uav_typed_indexable(buffer)(float,float,float,float) r0, vThreadID.x, u1
store_uav_typed u0, vThreadID.x, r0
ret
