cs_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_structured u0, 16
dcl_uav_structured u1, 16
dcl_uav_structured u2, 16
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 8, 1, 1
imm_atomic_alloc r0.x, u0
utof r1.x, vThreadID.x
mov r1.yzw, l(0, 1, 2, 3)
store_structured u0, r0.x, l(0), r1
imm_atomic_consume r0.x, u1
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r0, r0.x, l(0), u1
store_structured u2, vThreadID.x, l(0), r0
ret
