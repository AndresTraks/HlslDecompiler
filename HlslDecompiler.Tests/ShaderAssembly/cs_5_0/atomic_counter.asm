cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 4
dcl_uav_structured u0, 4
dcl_uav_structured u1, 4
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 64, 1, 1
ld_structured r0.x, vThreadID.x, l(0), t0.x
imm_atomic_iadd r0.x, u0, l(0, 0, 0, 0), r0.x
imm_atomic_exch r1.x, u0, l(1, 0, 0, 0), r0.x
iadd r0.x, r0.x, r1.x
store_structured u1.x, vThreadID.x, l(0), r0.x
ret
