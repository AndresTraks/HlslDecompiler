cs_4_1
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 16
dcl_uav_structured u0, 16
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 64, 1, 1
ld_structured r0, vThreadID.x, l(0), t0
add r0, r0, r0
store_structured u0, vThreadID.x, l(0), r0
ret
