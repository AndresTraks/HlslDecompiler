cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_uav_structured u0, 4
dcl_temps 1
dcl_thread_group 1, 1, 1
bfi r0.x, l(31), l(1), cb0[0].x, l(1)
store_structured u0.x, l(0), l(0), r0.x
bfi r0.x, l(24), l(8), cb0[0].y, cb0[0].x
store_structured u0.x, l(1), l(0), r0.x
ret
