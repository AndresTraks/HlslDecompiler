cs_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_structured u0, 20
dcl_uav_structured u1, 16
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 32, 1, 1
imm_atomic_consume r0.x, u0
ld_structured_indexable(structured_buffer, stride=20)(mixed,mixed,mixed,mixed) r1.x, r0.x, l(0), u0.x
ld_structured_indexable(structured_buffer, stride=20)(mixed,mixed,mixed,mixed) r1.y, r0.x, l(4), u0.x
ld_structured_indexable(structured_buffer, stride=20)(mixed,mixed,mixed,mixed) r1.z, r0.x, l(8), u0.x
ld_structured_indexable(structured_buffer, stride=20)(mixed,mixed,mixed,mixed) r1.w, r0.x, l(12), u0.x
ld_structured_indexable(structured_buffer, stride=20)(mixed,mixed,mixed,mixed) r0.x, r0.x, l(16), u0.x
itof r0.x, r0.x
mul r0, r0.x, r1
store_structured u1, vThreadID.x, l(0), r0
ret
