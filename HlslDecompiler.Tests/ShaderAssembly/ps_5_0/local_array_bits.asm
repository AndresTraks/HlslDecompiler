ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v0
dcl_input_ps constant v1.x
dcl_output o0
dcl_temps 1
dcl_indexableTemp x0[4], 4
dcl_indexableTemp x1[4], 4
mov x0[0].x, v0.x
mov x0[1].x, v0.y
mov x0[2].x, v0.z
mov x0[3].x, v0.w
mov x1[0].x, v1.x
ishl r0.x, v1.x, l(1)
mov x1[1].x, r0.x
imul null, r0.x, v1.x, l(3)
mov x1[2].x, r0.x
xor r0.x, v1.x, l(7)
mov x1[3].x, r0.x
and r0.x, v1.x, l(3)
mov r0.y, x0[r0.x].x
add r0.y, r0.y, l(1)
mov x0[r0.x].x, r0.y
mov o0.x, r0.y
iadd r0.yz, v1.xx, l(0, 1, 2, 0)
and r0.yz, r0.yz, l(0, 3, 3, 0)
mov r0.y, x0[r0.y].x
mov o0.w, x0[0].x
ubfe r0.w, l(2), l(2), v1.x
mov x1[r0.w].x, r0.y
mov r0.x, x1[r0.x].x
mov o0.z, x1[r0.z].x
utof o0.y, r0.x
ret
