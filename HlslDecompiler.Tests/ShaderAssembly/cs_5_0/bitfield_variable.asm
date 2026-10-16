cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_structured t0, 4
dcl_uav_structured u0, 4
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 16, 1, 1
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, vThreadID.x, l(0), t0.x
ushr r0.y, r0.x, cb0[0].x
bfi r0.x, cb0[0].y, l(4), r0.y, r0.x
store_structured u0.x, vThreadID.x, l(0), r0.x
ret
