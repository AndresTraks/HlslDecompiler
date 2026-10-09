ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[16], dynamicIndexed
dcl_input_ps linear v0
dcl_input_ps constant v1.xy
dcl_input_ps linear v2.xy
dcl_output o0
dcl_temps 2
ishl r0.xy, v1.xy, l(2, 2, 0, 0)
mul r1.xyz, v0.yyy, cb0[r0.y + 1].xyz
mad r1.xyz, cb0[r0.y].xyz, v0.xxx, r1.xyz
mad r1.xyz, cb0[r0.y + 2].xyz, v0.zzz, r1.xyz
mad r0.yzw, cb0[r0.y + 3].xyz, v0.www, r1.xyz
mul r0.yzw, r0.yzw, v2.yyy
mul r1.xyz, v0.yyy, cb0[r0.x + 1].xyz
mad r1.xyz, cb0[r0.x].xyz, v0.xxx, r1.xyz
mad r1.xyz, cb0[r0.x + 2].xyz, v0.zzz, r1.xyz
mad r1.xyz, cb0[r0.x + 3].xyz, v0.www, r1.xyz
mad o0.xyz, r1.xyz, v2.xxx, r0.yzw
mov o0.w, l(1)
ret
