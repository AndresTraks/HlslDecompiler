ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input_ps linear v0.xyz
dcl_output o0
dcl_temps 1
add r0.x, |cb0[0].w|, l(1)
rsq r0.x, r0.x
add r0.yzw, -v0.xyz, cb0[0].xyz
mul o0.xyz, r0.xxx, r0.yzw
mov o0.w, l(1)
ret
