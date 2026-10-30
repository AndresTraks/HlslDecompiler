cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_uav_structured u0, 16
dcl_temps 1
dcl_thread_group 1, 1, 1
ult r0.x, l(2147483647), cb0[0].x
ilt r0.y, l(-1), cb0[0].y
lt r0.z, cb0[0].z, l(0.5)
uge r0.w, cb0[0].x, l(7)
and r0, r0, l(1, 1, 1, 1)
store_structured u0, l(0), l(0), r0
ret
