cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 4
dcl_uav_structured u0, 4
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 64, 1, 1
ld_structured r0.x, vThreadID.x, l(0), t0.x
atomic_umin u0, l(0, 0, 0, 0), r0.x
atomic_umax u0, l(1, 0, 0, 0), r0.x
atomic_xor u0, l(2, 0, 0, 0), r0.x
ret
