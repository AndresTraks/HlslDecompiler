cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 16
dcl_uav_structured u0, 16
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 64, 1, 1
ld_structured r0, vThreadID.x, l(0), t0
lt r1.x, l(0.5), r0.w
if_nz r1.x
imm_atomic_alloc r1.x, u0
store_structured u0, r1.x, l(0), r0
endif
ret
