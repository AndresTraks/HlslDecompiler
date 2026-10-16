vs_5_0
dcl_globalFlags refactoringAllowed
dcl_input v0
dcl_input_sgv v1.x, vertex_id
dcl_output_siv o0, position
dcl_output o1
dcl_temps 1
dcl_indexableTemp x0[3], 4
dcl_indexableTemp x1[3], 4
mov x0[0].x, v1.x
imul null, r0.x, v1.x, l(7)
mov x0[1].x, r0.x
mov x0[2].x, v0.x
mov x1[0].x, v0.y
mov x1[1].x, v0.z
mov x1[2].x, v1.x
iadd r0.xy, v1.xx, l(1, 2, 0, 0)
udiv null, r0.xy, r0.xy, l(3, 3, 0, 0)
mov o0.y, x0[r0.x].x
mov o1.y, x1[r0.y].x
udiv null, r0.x, v1.x, l(3)
mov o0.x, x1[r0.x].x
mov o1.x, x0[r0.x].x
mov o0.zw, v0.zw
ushr o1.z, v0.x, l(3)
mov o1.w, l(1)
ret
