ps_4_0
dcl_constantbuffer CB0[5], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 3
dcl_indexableTemp x0[4], 4
mov x0[0], v0
add r0, v0.yzwx, cb0[1]
mov x0[1], r0
mul r0, v0, cb0[2]
mov x0[2], r0
add r0, -v0, cb0[3]
mov x0[3], r0
mov r0, l(0, 0, 0, 0)
mov r1.x, l(0)
loop
uge r1.y, r1.x, cb0[4].x
breakc_nz r1.y
and r1.y, r1.x, l(3)
mov r2, x0[r1.y]
add r0, r0, r2
iadd r1.x, r1.x, l(1)
endloop
mov o0, r0
ret
