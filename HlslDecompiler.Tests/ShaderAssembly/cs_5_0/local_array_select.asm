cs_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_structured u0, 16
dcl_uav_structured u1, 4
dcl_input vThreadID.x
dcl_temps 2
dcl_indexableTemp x0[4], 4
dcl_indexableTemp x1[4], 4
dcl_thread_group 8, 1, 1
mov x0[0].x, vThreadID.x
imul null, r0.x, vThreadID.x, l(3)
mov x0[1].x, r0.x
xor r0.x, vThreadID.x, l(5)
mov x0[2].x, r0.x
iadd r0.xyz, vThreadID.xxx, l(9, 1, 2, 0)
mov x0[3].x, r0.x
and r0.xy, r0.yz, l(3, 3, 0, 0)
mov x1[0].x, l(1065353216)
mov x1[1].x, l(1073741824)
mov x1[2].x, l(1077936128)
utof r0.z, vThreadID.x
mov x1[3].x, r0.z
mov r1.x, x1[r0.x].x
and r0.z, vThreadID.x, l(3)
mov x0[r0.z].x, r1.x
mov r1.w, x1[r0.z].x
mov r0.y, x0[r0.y].x
mov r0.x, x0[r0.x].x
iadd r0.x, r0.x, l(1077936128)
store_structured u1.x, vThreadID.x, l(0), r0.x
and r0.x, r0.y, l(1)
mov r0.y, x0[1].x
mov r0.z, x0[3].x
itof r1.z, r0.z
movc r1.y, r0.x, l(1), r0.y
store_structured u0, vThreadID.x, l(0), r1
ret
