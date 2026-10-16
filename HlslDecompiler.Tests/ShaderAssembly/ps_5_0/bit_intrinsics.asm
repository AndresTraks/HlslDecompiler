ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input_ps constant v0
dcl_output o0
dcl_temps 2
firstbit_shi r0.x, cb0[0].y
iadd r0.y, -r0.x, l(31)
ieq r0.x, r0.x, l(-1)
utof r0.y, r0.y
movc r0.x, r0.x, l(4294967296.0), r0.y
firstbit_hi r0.y, v0.z
iadd r0.y, -r0.y, l(31)
movc r0.y, v0.z, r0.y, l(-1)
utof r1.z, r0.y
and r0.y, v0.x, cb0[0].x
countbits r0.y, r0.y
utof r1.x, r0.y
bfrev r0.y, v0.y
ushr r0.y, r0.y, l(28)
utof r1.y, r0.y
or r0.y, v0.w, l(256)
firstbit_lo r0.y, r0.y
utof r1.w, r0.y
add o0, r0.x, r1
ret
