ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[12], dynamicIndexed
dcl_input_ps linear v0
dcl_input_ps constant v1.xy
dcl_input_ps linear v2.xy
dcl_output o0
dcl_temps 2
imul null, r0.xy, v1.xy, l(3, 3, 0, 0)
dp4 r1.x, cb0[r0.y], v0
dp4 r1.y, cb0[r0.y + 1], v0
dp4 r1.z, cb0[r0.y + 2], v0
mul r0.yzw, r1.xyz, v2.yyy
dp4 r1.x, cb0[r0.x], v0
dp4 r1.y, cb0[r0.x + 1], v0
dp4 r1.z, cb0[r0.x + 2], v0
mad o0.xyz, r1.xyz, v2.xxx, r0.yzw
mov o0.w, l(1)
ret
