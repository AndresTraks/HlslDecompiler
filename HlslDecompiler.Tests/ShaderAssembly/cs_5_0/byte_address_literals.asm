cs_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_raw u0
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 8, 1, 1
ishl r0.x, vThreadID.x, l(2)
store_raw u0.x, r0.x, l(7)
store_raw u0.xy, l(64), l(11, 13, 0, 0)
ret
