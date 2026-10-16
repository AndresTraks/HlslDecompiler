cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 24
dcl_uav_structured u0, 24
dcl_input vThreadID.x
dcl_temps 3
dcl_thread_group 32, 1, 1
ld_structured_indexable(structured_buffer, stride=24)(mixed,mixed,mixed,mixed) r0, vThreadID.x, l(0), t0
and r1.x, r0.w, l(0x00000001)
if_nz r1.x
ld_structured_indexable(structured_buffer, stride=24)(mixed,mixed,mixed,mixed) r1.xy, vThreadID.xx, l(16), t0.xy
imm_atomic_alloc r2.x, u0
mul r0, r0, l(2, 2, 2, 1)
store_structured u0, r2.x, l(0), r0
store_structured u0.xy, r2.xx, l(16), r1.yx
endif
ret
