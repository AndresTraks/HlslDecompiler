cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_texture2d (float,float,float,float) t0
dcl_uav_typed_texture2d (float,float,float,float) u0
dcl_input vThreadID.xy
dcl_temps 1
dcl_thread_group 8, 8, 1
mov r0.xy, vThreadID.xy
mov r0.zw, l(0, 0, 0, 0)
ld r0, r0, t0
add r0, r0, r0
store_uav_typed u0, vThreadID.xyyy, r0
ret
