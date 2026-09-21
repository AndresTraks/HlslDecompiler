ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input_ps constant v0.xy
dcl_output o0
dcl_temps 1
ishl r0.x, v0.y, l(2)
iadd o0.x, r0.x, cb0[0].x
iadd o0.y, -v0.x, cb0[0].x
xor o0.z, v0.y, cb0[0].y
and o0.w, v0.y, v0.x
ret
