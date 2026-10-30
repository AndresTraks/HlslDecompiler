cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[19], dynamicIndexed
dcl_uav_structured u0, 16
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 4, 1, 1
ushr r0.x, vThreadID.x, l(1)
and r0.y, cb0[0].x, l(1)
imul null, r0.xy, r0.xy, l(3, 3, 0, 0)
add r0, cb0[r0.x + 5], cb0[r0.y + 9]
or r1.x, cb0[0].x, l(1)
imad r1.x, r1.x, l(3), l(3)
add r0, r0, cb0[r1.x + 1]
store_structured u0, vThreadID.x, l(0), r0
ret
