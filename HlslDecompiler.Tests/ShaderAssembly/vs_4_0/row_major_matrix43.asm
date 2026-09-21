vs_4_0
dcl_constantbuffer CB0[16], immediateIndexed
dcl_input v0
dcl_output o0
dcl_temps 2
mul r0.xyz, v0.yyy, cb0[1].xyz
mad r0.xyz, v0.xxx, cb0[0].xyz, r0.xyz
mad r0.xyz, v0.zzz, cb0[2].xyz, r0.xyz
mad r0.xyz, v0.www, cb0[3].xyz, r0.xyz
mul r1.xyz, v0.yyy, cb0[13].xyz
mad r1.xyz, v0.xxx, cb0[12].xyz, r1.xyz
mad r1.xyz, v0.zzz, cb0[14].xyz, r1.xyz
mad r1.xyz, v0.www, cb0[15].xyz, r1.xyz
add o0.xyz, r0.xyz, r1.xyz
mov o0.w, l(1)
ret
