cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_structured t0, 4
dcl_uav_structured u0, 4
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 8, 1, 1
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, vThreadID.x, l(0), t0.x
iadd r0.x, r0.x, l(-3)
ige r0.x, r0.x, cb0[0].y
iadd r0.y, vThreadID.x, l(-4)
ilt r0.z, r0.y, cb0[0].x
and r0.w, r0.z, l(1)
bfi r0.z, l(1), l(0), r0.z, l(2)
movc r0.x, r0.x, r0.z, r0.w
iadd r0.z, r0.x, l(4)
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.w, l(0), l(0), t0.x
ilt r0.y, r0.w, r0.y
movc r0.x, r0.y, r0.z, r0.x
store_structured u0.x, vThreadID.x, l(0), r0.x
ret
