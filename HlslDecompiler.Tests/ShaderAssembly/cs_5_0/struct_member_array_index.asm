cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[41], dynamicIndexed
dcl_uav_structured u0, 16
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 8, 1, 1
udiv null, r0.x, vThreadID.x, cb0[40].x
imul null, r0.x, r0.x, l(5)
dp4 r1.x, l(1, 2, 3, 1), cb0[r0.x]
dp4 r1.y, l(1, 2, 3, 1), cb0[r0.x + 1]
dp4 r1.z, l(1, 2, 3, 1), cb0[r0.x + 2]
dp4 r1.w, l(1, 2, 3, 1), cb0[r0.x + 3]
mul r0, r1, cb0[r0.x + 4]
store_structured u0, vThreadID.x, l(0), r0
ret
