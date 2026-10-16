cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 24
dcl_uav_structured u0, 24
dcl_input vThreadID.x
dcl_temps 3
dcl_thread_group 32, 1, 1
imm_atomic_alloc r0.x, u0
ld_structured_indexable(structured_buffer, stride=24)(mixed,mixed,mixed,mixed) r1, vThreadID.x, l(0), t0
add r2.xyz, r1.xyz, r1.xyz
iadd r2.w, r1.w, l(1)
store_structured u0, r0.x, l(0), r2
ld_structured_indexable(structured_buffer, stride=24)(mixed,mixed,mixed,mixed) r0.yz, vThreadID.xx, l(16), t0.xy
store_structured u0.xy, r0.xx, l(16), r0.zy
ret
