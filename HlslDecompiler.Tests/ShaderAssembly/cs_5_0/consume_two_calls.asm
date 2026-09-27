cs_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_structured u0, 16
dcl_uav_structured u1, 16
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 1, 1, 1
imm_atomic_consume r0.x, u0
imm_atomic_consume r1.x, u0
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r0.x, r0.x, l(0), u0.x
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r0.y, r1.x, l(12), u0.x
mov r0.zw, l(0, 0, 0, 1)
store_structured u1, vThreadID.x, l(0), r0
ret
