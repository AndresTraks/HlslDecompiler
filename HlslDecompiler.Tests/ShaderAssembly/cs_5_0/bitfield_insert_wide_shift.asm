cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_uav_structured u0, 4
dcl_temps 1
dcl_thread_group 1, 1, 1
and r0.x, cb0[0].z, l(1)
ishl r0.yz, l(0, 2147483647, 33554431, 0), r0.xx
bfi r0.xw, l(31, 0, 0, 25), r0.xx, cb0[0].yy, l(0, 0, 0, 0)
not r0.yz, r0.yz
and r0.yz, r0.yz, cb0[0].xx
or r0.xy, r0.xw, r0.yz
store_structured u0.x, l(0), l(0), r0.x
store_structured u0.x, l(1), l(0), r0.y
ret
