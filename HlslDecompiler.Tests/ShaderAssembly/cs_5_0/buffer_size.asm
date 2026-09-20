cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 16
dcl_resource_raw t1
dcl_uav_structured u0, 8
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 64, 1, 1
bufinfo r0.x, t0.x
iadd r0.x, r0.x, l(16)
bufinfo r0.y, t1.x
store_structured u0.xy, vThreadID.xx, l(0), r0.xy
ret
