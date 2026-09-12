ps_4_0
dcl_constantbuffer cb0[2], immediateIndexed
dcl_output o0
dcl_temps 2
xor r0.xy, cb0[1].xw, l(3, 3, 0, 0)
and r0.xy, r0.xy, l(-2147483648, -2147483648, 0, 0)
imax r0.zw, cb0[1].xw, -cb0[1].xw
udiv r1.xy, null, r0.zw, l(3, 3, 0, 0)
udiv null, r0.zw, r0.zw, l(0, 0, 7, 7)
ineg r1.zw, r1.xy
movc r0.xy, r0.xy, r1.zw, r1.xy
ineg r1.xy, r0.zw
and r1.zw, cb0[1].xw, l(0, 0, -2147483648, -2147483648)
movc r0.zw, r1.zw, r1.xy, r0.zw
iadd r0.xy, r0.xy, r0.zw
itof o0.zw, r0.xy
ishl r0.x, cb0[0].x, l(3)
ishr r0.y, cb0[0].x, l(2)
or r0.x, r0.y, r0.x
itof o0.x, r0.x
ushr r0.x, cb0[0].y, l(1)
and r0.x, r0.x, l(15)
utof o0.y, r0.x
ret
