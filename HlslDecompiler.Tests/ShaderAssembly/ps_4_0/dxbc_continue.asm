ps_4_0
dcl_input_ps linear v0
dcl_output o0
dcl_temps 2
mov r0, l(0, 0, 0, 0)
mov r1.x, l(0)
loop
ige r1.y, r1.x, l(8)
breakc_nz r1.y
ieq r1.y, r1.x, l(3)
if_nz r1.y
mov r1.x, l(4)
continue
endif
add r0, r0, v0
iadd r1.x, r1.x, l(1)
endloop
mov o0, r0
ret
