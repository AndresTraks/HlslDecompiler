cs_4_1
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 4
dcl_uav_structured u0, 4
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 256, 1, 1
ld_structured r0.x, vThreadID.x, l(0), t0.x
add r0.x, r0.x, r0.x
store_structured u0.x, vThreadID.x, l(0), r0.x
ret
