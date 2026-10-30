ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[2], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 3
div r0, v0, cb0[1]
and r1, r0, l(2147483647, 2147483647, 2147483647, 2147483647)
ieq r1, r1, l(2139095040, 2139095040, 2139095040, 2139095040)
ne r2, r0, r0
or r1, r1, r2
or r2.xy, r1.zw, r1.xy
movc r0, r1, cb0[0], r0
or r1.x, r2.y, r2.x
and r1.x, r1.x, l(1)
itof o0.x, r1.x
add o0.z, r0.y, r0.x
max o0.w, r0.w, r0.z
mov o0.y, l(1)
ret
