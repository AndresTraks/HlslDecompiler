ps_5_0
dcl_globalFlags refactoringAllowed | enable11_1ShaderExtensions
dcl_constantbuffer CB0[2], immediateIndexed
dcl_output o0
dcl_temps 1
firstbit_hi r0.x, cb0[0].x
iadd r0.x, -r0.x, l(31)
movc r0.x, cb0[0].x, r0.x, l(-1)
countbits r0.y, cb0[0].x
iadd r0.x, r0.x, r0.y
firstbit_lo r0.y, cb0[0].x
iadd r0.x, r0.y, r0.x
utof o0.x, r0.x
ushr r0.x, cb0[1].y, l(24)
bfi r0.y, l(24), l(8), cb0[1].z, r0.x
mov r0.x, cb0[1].y
msad r0.xy, cb0[1].xx, r0.xy, cb0[1].xw
iadd r0.x, r0.y, r0.x
utof o0.w, r0.x
bfrev r0.x, cb0[0].x
and r0.x, r0.x, l(255)
utof o0.y, r0.x
f16tof32 r0.xy, cb0[1].xy
add o0.z, r0.y, r0.x
ret
