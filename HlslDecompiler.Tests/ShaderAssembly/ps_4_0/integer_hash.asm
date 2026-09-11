ps_4_0
dcl_constantbuffer cb0[1], immediateIndexed
dcl_output o0
dcl_temps 1
mov r0.x, cb0[0].x
mov r0.y, l(0)
loop
uge r0.z, r0.y, cb0[0].y
breakc_nz r0.z
imad r0.z, r0.x, l(1664525), l(1013904223)
ushr r0.w, r0.x, l(16)
xor r0.x, r0.w, r0.z
iadd r0.y, r0.y, l(1)
endloop
and r0.y, r0.x, l(255)
utof r0.y, r0.y
mul o0.x, r0.y, l(0.00392156886)
ushr r0.y, r0.x, l(8)
ushr r0.z, r0.x, l(16)
and r0.xy, r0.yz, l(255, 255, 0, 0)
utof r0.xy, r0.xy
mul o0.yz, r0.xy, l(0, 0.00392156886, 0.00392156886, 0)
mov o0.w, l(1)
ret
