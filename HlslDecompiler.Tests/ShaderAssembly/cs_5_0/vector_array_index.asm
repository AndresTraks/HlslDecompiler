cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 64
dcl_uav_structured u0, 64
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 16, 1, 1
ld_structured_indexable(structured_buffer, stride=64)(mixed,mixed,mixed,mixed) r0.x, vThreadID.x, l(0), t0.x
udiv null, r0.x, r0.x, l(3)
ishl r0.y, r0.x, l(4)
iadd r0.x, r0.x, l(1)
udiv null, r0.x, r0.x, l(3)
ishl r0.x, r0.x, l(4)
iadd r0.xy, r0.xy, l(16, 16, 0, 0)
ld_structured_indexable(structured_buffer, stride=64)(mixed,mixed,mixed,mixed) r1, vThreadID.x, r0.y, t0
add r1, r1, r1
store_structured u0, vThreadID.x, r0.x, r1
ret
