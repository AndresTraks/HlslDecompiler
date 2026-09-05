ps_4_0
dcl_constantbuffer cb0[1], immediateIndexed
dcl_output o0
dcl_temps 3
xor r0.x, cb0[0].y, cb0[0].x
and r0.x, r0.x, l(-2,1474836E+09)
imax r0.yz, cb0[0].xy, -cb0[0].xy
udiv r1, r2, r0.y, r0.z
ineg r0.y, r1.x
movc r0.x, r0.x, r0.y, r1.x
ineg r0.y, r2.x
and r0.z, cb0[0].x, l(-2,1474836E+09)
movc r0.y, r0.z, r0.y, r2.x
itof r0.xy, r0.xy
add o0, r0.y, r0.x
ret
