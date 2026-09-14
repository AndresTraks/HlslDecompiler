ps_4_0
dcl_constantbuffer cb0[1], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 2
xor r0.x, cb0[0].x, l(7)
and r0.x, r0.x, l(-2147483648)
imax r0.y, cb0[0].x, -cb0[0].x
udiv r0.z, null, r0.y, l(7)
udiv null, r0.y, r0.y, l(13)
ineg r0.w, r0.z
movc r0.x, r0.x, r0.w, r0.z
itof r1.x, r0.x
ineg r0.x, r0.y
and r0.z, cb0[0].x, l(-2147483648)
movc r0.x, r0.z, r0.x, r0.y
itof r1.y, r0.x
udiv r0.x, null, cb0[0].y, l(10)
utof r1.z, r0.x
udiv null, r0.x, cb0[0].y, l(6)
utof r1.w, r0.x
mul o0, r1, v0
ret
