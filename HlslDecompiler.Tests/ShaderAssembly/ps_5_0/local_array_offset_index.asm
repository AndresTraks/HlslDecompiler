ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v0
dcl_input_ps constant v1
dcl_output o0
dcl_temps 2
dcl_indexableTemp x0[12], 4
mov x0[0].x, l(0)
mov x0[1].x, v0.x
add r0, v0, v0
mov x0[2].x, r0.x
mov x0[3].x, l(0)
mov x0[4].x, v0.y
mov x0[5].x, r0.y
mov x0[6].x, l(0)
mov x0[7].x, v0.z
mov x0[8].x, r0.z
mov x0[9].x, l(0)
mov x0[10].x, v0.w
mov x0[11].x, r0.w
imax r0.xy, v1.yw, -v1.yw
udiv null, r0.xy, r0.xy, l(3, 3, 0, 0)
ineg r0.zw, r0.xy
and r1, v1.yxzw, l(-2147483648, 3, 3, -2147483648)
movc r0.xy, r1.xw, r0.zw, r0.xy
imad r0.x, r1.y, l(3), r0.x
imul null, r0.z, r1.z, l(3)
mov x0[r0.x].x, l(9)
mov o0.y, x0[r0.y + 3].x
mov o0.x, x0[r0.z + 1].x
mov o0.z, x0[11].x
mov o0.w, x0[0].x
ret
