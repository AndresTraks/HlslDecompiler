ps_4_0
dcl_constantbuffer cb0[1], immediateIndexed
dcl_input_ps constant v0
dcl_output o0
dcl_temps 1
dcl_indexableTemp x0[4], 4
mov x0[0].x, v0.x
ishl r0.x, v0.y, l(1)
mov x0[1].x, r0.x
iadd r0.x, v0.z, l(1)
mov x0[2].x, r0.x
mov x0[3].x, v0.w
iadd r0.x, v0.x, cb0[0].x
and r0.x, r0.x, l(3)
mov r0.y, x0[r0.x].x
iadd r0.y, r0.y, l(5)
mov x0[r0.x].x, r0.y
itof o0.xw, r0.yx
iadd r0.y, r0.x, l(1)
and r0.x, r0.y, l(3)
mov r0.x, x0[r0.x].x
mov r0.y, x0[3].x
itof o0.yz, r0.yx
ret
