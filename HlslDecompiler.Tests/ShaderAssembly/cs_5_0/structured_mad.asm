cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t1, 16
dcl_uav_structured u1, 16
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 32, 1, 1
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r0, vThreadID.x, l(0), t1
mad r0, r0, l(2, 2, 2, 2), l(1, 1, 1, 1)
store_structured u1, vThreadID.x, l(0), r0
ret
