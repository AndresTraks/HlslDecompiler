cs_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_structured u0, 4
dcl_uav_structured u1, 4
dcl_temps 2
dcl_thread_group 8, 1, 1
imm_atomic_consume r0.x, u1
imm_atomic_alloc r1.x, u0
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, r0.x, l(0), u1.x
ishl r0.x, r0.x, l(1)
store_structured u0.x, r1.x, l(0), r0.x
ret
