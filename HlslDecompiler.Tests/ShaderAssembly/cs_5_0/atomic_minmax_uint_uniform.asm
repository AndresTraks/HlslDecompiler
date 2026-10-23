cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_uav_structured u0, 4
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 8, 1, 1
and r0.x, vThreadID.x, l(1)
mov r0.yw, l(0, 0, 0, 0)
imm_atomic_imax r1.x, u0, r0.xyxx, cb0[0].x
bfi r0.z, l(1), l(0), vThreadID.x, l(2)
atomic_imin u0, r0.zwzz, cb0[0].y
iadd r0.x, vThreadID.x, l(4)
store_structured u0.x, r0.x, l(0), r1.x
ret
