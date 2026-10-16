cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 24
dcl_uav_structured u0, 24
dcl_input vThreadID.xy
dcl_temps 1
dcl_thread_group 16, 1, 1
bfi r0.xy, l(2, 1, 0, 0), l(2, 2, 0, 0), vThreadID.yy, l(0, 0, 0, 0)
iadd r0.y, r0.y, l(16)
ld_structured_indexable(structured_buffer, stride=24)(mixed,mixed,mixed,mixed) r0.y, vThreadID.x, r0.y, t0.x
utof r0.y, r0.y
iadd r0.z, -r0.x, l(12)
ld_structured_indexable(structured_buffer, stride=24)(mixed,mixed,mixed,mixed) r0.z, vThreadID.x, r0.z, t0.x
add r0.y, r0.y, r0.z
store_structured u0.x, vThreadID.x, r0.x, r0.y
ret
