cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 20
dcl_uav_structured u0, 20
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 32, 1, 1
ld_structured_indexable(structured_buffer, stride=20)(mixed,mixed,mixed,mixed) r0.x, vThreadID.x, l(16), t0.x
and r0.y, r0.x, l(3)
iadd r0.z, r0.y, l(1)
bfi r0.xz, l(2, 0, 2, 0), l(2, 0, 2, 0), r0.xz, l(0, 0, 0, 0)
ld_structured_indexable(structured_buffer, stride=20)(mixed,mixed,mixed,mixed) r0.z, vThreadID.x, r0.z, t0.x
add r0.z, r0.z, r0.z
store_structured u0.x, vThreadID.x, r0.x, r0.z
store_structured u0.x, vThreadID.x, l(16), r0.y
ret
