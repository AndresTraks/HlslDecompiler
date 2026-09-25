cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 80
dcl_uav_structured u0, 16
dcl_input vThreadID.x
dcl_temps 3
dcl_thread_group 8, 1, 1
ld_structured_indexable(structured_buffer, stride=80)(mixed,mixed,mixed,mixed) r0, vThreadID.x, l(0), t0
ld_structured_indexable(structured_buffer, stride=80)(mixed,mixed,mixed,mixed) r1, vThreadID.x, l(64), t0
dp4 r0.x, r1, r0
ld_structured_indexable(structured_buffer, stride=80)(mixed,mixed,mixed,mixed) r2, vThreadID.x, l(16), t0
dp4 r0.y, r1, r2
ld_structured_indexable(structured_buffer, stride=80)(mixed,mixed,mixed,mixed) r2, vThreadID.x, l(32), t0
dp4 r0.z, r1, r2
ld_structured_indexable(structured_buffer, stride=80)(mixed,mixed,mixed,mixed) r2, vThreadID.x, l(48), t0
dp4 r0.w, r1, r2
store_structured u0, vThreadID.x, l(0), r0
ret
