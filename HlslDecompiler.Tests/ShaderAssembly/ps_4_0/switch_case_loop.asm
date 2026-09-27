ps_4_0
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 3
mov r0, l(0, 0, 0, 0)
mov r1.x, l(0)
loop
uge r1.y, r1.x, cb0[0].x
breakc_nz r1.y
and r1.y, r1.x, l(1)
switch r1.y
case l(0)
add r2, r0, v0
break
default
mov r2, r0
mov r1.y, l(0)
loop
uge r1.z, r1.y, cb0[0].y
breakc_nz r1.z
utof r1.z, r1.y
mad r2, v0, r1.z, r2
iadd r1.y, r1.y, l(1)
endloop
break
endswitch
lt r1.y, l(50), r2.y
if_nz r1.y
mov r0, r2
break
endif
mov r0, r2
iadd r1.x, r1.x, l(1)
endloop
mov o0, r0
ret
