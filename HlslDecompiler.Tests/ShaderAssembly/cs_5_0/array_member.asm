cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 76
dcl_uav_structured u0, 16
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 8, 1, 1
ld_structured_indexable(structured_buffer, stride=76)(mixed,mixed,mixed,mixed) r0.x, vThreadID.x, l(72), t0.x
utof r0.w, r0.x
ld_structured_indexable(structured_buffer, stride=76)(mixed,mixed,mixed,mixed) r0.x, vThreadID.x, l(16), t0.x
ld_structured_indexable(structured_buffer, stride=76)(mixed,mixed,mixed,mixed) r0.y, vThreadID.x, l(24), t0.x
ld_structured_indexable(structured_buffer, stride=76)(mixed,mixed,mixed,mixed) r0.z, vThreadID.x, l(64), t0.x
store_structured u0, vThreadID.x, l(0), r0
ret
