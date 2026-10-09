ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 2
lt r0.xyz, cb0[0].xyz, v0.xyz
and r0.w, r0.y, r0.x
and r0.w, r0.z, r0.w
not r0.w, r0.w
or r0.x, r0.y, r0.x
or r0.x, r0.z, r0.x
and r0.x, r0.w, r0.x
max r0.yzw, v0.xyz, cb0[0].xyz
add r1.xyz, r0.yzw, r0.yzw
movc o0.xyz, r0.xxx, r1.xyz, r0.yzw
and r0.x, v0.w, l(2147483647)
ieq r0.x, r0.x, l(2139095040)
movc o0.w, r0.x, l(1), v0.w
ret
