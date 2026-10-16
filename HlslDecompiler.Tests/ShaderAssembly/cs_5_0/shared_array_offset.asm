cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 20
dcl_uav_structured u0, 20
dcl_input vThreadID.xy
dcl_temps 1
dcl_thread_group 16, 1, 1
bfi r0.x, l(2), l(2), vThreadID.y, l(0)
iadd r0.x, r0.x, l(4)
ld_structured_indexable(structured_buffer, stride=20)(mixed,mixed,mixed,mixed) r0.y, vThreadID.x, r0.x, t0.x
ushr r0.y, r0.y, l(1)
store_structured u0.x, vThreadID.x, r0.x, r0.y
ld_structured_indexable(structured_buffer, stride=20)(mixed,mixed,mixed,mixed) r0.x, vThreadID.x, l(0), t0.x
store_structured u0.x, vThreadID.x, l(0), r0.x
ret
