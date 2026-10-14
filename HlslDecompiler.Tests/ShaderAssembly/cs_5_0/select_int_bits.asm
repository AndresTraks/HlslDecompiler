cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 16
dcl_resource_structured t1, 16
dcl_uav_structured u0, 16
dcl_input vThreadID.x
dcl_temps 3
dcl_thread_group 32, 1, 1
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r0, vThreadID.x, l(0), t1
imad r0, r0, l(3, 3, 3, 3), l(1, 1, 1, 1)
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r1, vThreadID.x, l(0), t0
lt r2, l(0.5, 0.5, 0.5, 0.5), r1
movc r0, r2, r1, r0
add r0, r0, r0
store_structured u0, vThreadID.x, l(0), r0
ret
