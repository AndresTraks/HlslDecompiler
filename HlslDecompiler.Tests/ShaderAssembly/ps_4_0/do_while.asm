ps_4_0
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 3
mul r0, v0, cb0[0].x
ftoi r1.x, cb0[0].y
mov r2, r0
mov r1.y, l(1)
loop
ige r1.z, r1.y, r1.x
breakc_nz r1.z
mad r2, v0, cb0[0].x, r2
iadd r1.y, r1.y, l(1)
endloop
mov o0, r2
ret
