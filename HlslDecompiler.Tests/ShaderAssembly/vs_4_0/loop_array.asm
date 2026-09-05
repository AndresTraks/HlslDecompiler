vs_4_0
dcl_constantbuffer cb0[9], immediateIndexed
dcl_output_siv o0, position
dcl_temps 2
mov r0, l(0, 0, 0, 0)
mov r1.x, l(0)
loop
ige r1.y, r1.x, cb0[8].x
breakc_nz r1.y
and r1.y, r1.x, l(7)
add r0, r0, cb0[0]
iadd r1.x, r1.x, l(1)
endloop
mov o0, r0
ret
