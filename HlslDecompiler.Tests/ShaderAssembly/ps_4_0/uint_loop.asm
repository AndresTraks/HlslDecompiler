ps_4_0
dcl_constantbuffer cb0[1], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 2
mov r0, l(0, 0, 0, 0)
mov r1.x, l(0)
loop
uge r1.y, r1.x, cb0[0].x
breakc_nz r1.y
ishl r1.y, r1.x, l(2)
utof r1.y, r1.y
mad r0, v0, r1.y, r0
iadd r1.x, r1.x, l(1)
endloop
mov o0, r0
ret
