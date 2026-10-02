cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_uav_structured u0, 4
dcl_uav_structured u1, 4
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 64, 1, 1
imm_atomic_consume r0.x, u0
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, r0.x, l(0), u0.x
mad r0.x, r0.x, cb0[0].x, r0.x
store_structured u1.x, vThreadID.x, l(0), r0.x
ret
