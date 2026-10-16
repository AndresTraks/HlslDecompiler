cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 52
dcl_uav_structured u0, 12
dcl_input vThreadID.x
dcl_temps 3
dcl_thread_group 32, 1, 1
ld_structured_indexable(structured_buffer, stride=52)(mixed,mixed,mixed,mixed) r0, vThreadID.x, l(0), t0
ld_structured_indexable(structured_buffer, stride=52)(mixed,mixed,mixed,mixed) r1.x, vThreadID.x, l(48), t0.x
utof r1.w, r1.x
utof r1.x, vThreadID.x
mov r1.yz, l(0, 1, 2, 0)
dp4 r0.x, r1, r0
ld_structured_indexable(structured_buffer, stride=52)(mixed,mixed,mixed,mixed) r2, vThreadID.x, l(16), t0
dp4 r0.y, r1, r2
ld_structured_indexable(structured_buffer, stride=52)(mixed,mixed,mixed,mixed) r2, vThreadID.x, l(32), t0
dp4 r0.z, r1, r2
store_structured u0.xyz, vThreadID.xxx, l(0), r0.xyz
ret
