cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 4
dcl_uav_structured u0, 4
dcl_uav_structured u1, 4
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 8, 1, 1
mov r0.y, l(0)
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, vThreadID.x, l(0), t0.x
imm_atomic_cmp_exch r1.x, u0, r0.xyxx, l(1), l(2)
store_structured u1.x, r0.x, l(0), r1.x
ret
