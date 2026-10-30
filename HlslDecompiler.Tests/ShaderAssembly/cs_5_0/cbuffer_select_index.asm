cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[5], dynamicIndexed
dcl_uav_structured u0, 16
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 4, 1, 1
ilt r0.x, vThreadID.x, cb0[0].x
movc r0.x, r0.x, l(3), l(1)
add r0, cb0[r0.x + 1], cb0[r0.x + 1]
store_structured u0, vThreadID.x, l(0), r0
ret
