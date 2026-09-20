cs_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_typed_buffer (float,float,float,float) u0
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 64, 1, 1
utof r0.x, vThreadID.x
mul r0.x, r0.x, l(0.25)
store_uav_typed u0, vThreadID.x, r0.x
ret
