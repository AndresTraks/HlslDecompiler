ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_output o0
dcl_temps 2
udiv null, r0.x, cb0[0].x, l(3)
utof o0.x, r0.x
udiv r0.x, null, cb0[0].x, l(5)
utof o0.y, r0.x
or r0.x, cb0[0].x, l(1)
udiv r0.x, r1.x, cb0[0].y, r0.x
utof o0.z, r1.x
utof o0.w, r0.x
ret
