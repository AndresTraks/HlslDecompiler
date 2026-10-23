cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_uav_structured u0, 4
dcl_uav_structured u1, 4
dcl_input vThreadID.x
dcl_temps 4
dcl_thread_group 8, 1, 1
iadd r0.xy, vThreadID.xx, l(-4, 2, 0, 0)
imm_atomic_imin r1.x, u0, l(0, 0, 0, 0), r0.x
iadd r0.zw, -vThreadID.xx, cb0[0].xy
imm_atomic_imax r2.x, u0, l(1, 0, 0, 0), r0.z
imm_atomic_umin r3.x, u1, l(0, 0, 0, 0), r0.w
iadd r0.z, r1.x, r2.x
iadd r0.z, r3.x, r0.z
atomic_umax u1, l(1, 0, 0, 0), r0.x
store_structured u0.x, r0.y, l(0), r0.z
ret
