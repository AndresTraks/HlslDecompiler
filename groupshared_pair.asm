cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 16
dcl_uav_structured u0, 16
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 32, 1, 1
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r0, vThreadID.x, l(0), t0
mad r1, r0, l(3, 3, 3, 3), l(1, 1, 1, 1)
div r0, r0, r1
store_structured u0, vThreadID.x, l(0), r0
ret
