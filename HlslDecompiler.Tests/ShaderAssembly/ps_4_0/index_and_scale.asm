ps_4_0
dcl_constantbuffer CB0[5], dynamicIndexed
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 2
mov r0, l(0, 0, 0, 0)
mov r1.x, l(0)
loop
uge r1.y, r1.x, l(4)
breakc_nz r1.y
imul null, r1.y, r1.x, cb0[4].x
and r1.y, r1.y, l(3)
utof r1.z, r1.x
add r1.z, r1.z, v0.x
mad r0, cb0[0], r1.z, r0
iadd r1.x, r1.x, l(1)
endloop
ushr r1.x, v0.y, l(23)
and r1.x, r1.x, l(255)
utof r1.x, r1.x
add o0, r0, r1.x
ret
