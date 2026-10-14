cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 16
dcl_uav_structured u0, 16
dcl_input vThreadID.x
dcl_temps 3
dcl_thread_group 32, 1, 1
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r0, vThreadID.x, l(0), t0
mul r1, r0, l(100, 100, 100, 100)
ftou r1, r1
lt r2, l(0.5, 0.5, 0.5, 0.5), r0
movc r0, r2, r0, r1
store_structured u0, vThreadID.x, l(0), r0
ret
