cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_uav_structured u0, 16
dcl_uav_structured u1, 16
dcl_temps 2
dcl_thread_group 64, 1, 1
imm_atomic_consume r0.x, u0
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r1.x, r0.x, l(0), u0.x
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r1.y, r0.x, l(4), u0.x
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r1.z, r0.x, l(8), u0.x
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r0.x, r0.x, l(12), u0.x
lt r0.y, cb0[0].x, r1.y
if_nz r0.y
iadd r1.w, r0.x, l(1000)
imm_atomic_alloc r0.x, u1
store_structured u1, r0.x, l(0), r1
endif
ret
