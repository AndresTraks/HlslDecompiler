ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_output o0
dcl_temps 2
mul r0.x, cb0[0].x, l(3)
sincos r0.x, r1.x, r0.x
mov o0.x, r0.x
mov o0.y, r1.x
mov o0.zw, l(0, 0, 0, 0)
ret
