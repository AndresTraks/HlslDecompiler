ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[2], immediateIndexed
dcl_output o0
dcl_temps 4
xor r0, cb0[0], cb0[1]
and r0, r0, l(-2147483648, -2147483648, -2147483648, -2147483648)
imax r1, cb0[0], -cb0[0]
imax r2, cb0[1], -cb0[1]
udiv r1, r2, r1, r2
ineg r3, r1
movc r0, r0, r3, r1
itof r0, r0
ineg r1, r2
and r3, cb0[0], l(-2147483648, -2147483648, -2147483648, -2147483648)
movc r1, r3, r1, r2
itof r1, r1
add o0, r0, r1
ret
