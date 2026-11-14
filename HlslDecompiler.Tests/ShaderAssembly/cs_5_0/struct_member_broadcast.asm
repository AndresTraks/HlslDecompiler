cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 20
dcl_uav_structured u0, 16
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 64, 1, 1
ld_structured_indexable(structured_buffer, stride=20)(mixed,mixed,mixed,mixed) r0, vThreadID.x, l(0), t0
ld_structured_indexable(structured_buffer, stride=20)(mixed,mixed,mixed,mixed) r1.x, vThreadID.x, l(16), t0.x
mul r0, r0, r1.x
store_structured u0, vThreadID.x, l(0), r0
ret
