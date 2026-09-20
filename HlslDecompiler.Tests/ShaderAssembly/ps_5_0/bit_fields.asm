ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_output o0
dcl_temps 1
ubfe r0.xy, l(5, 8, 0, 0), l(3, 12, 0, 0), cb0[0].xx
utof o0.xy, r0.xy
ibfe r0.x, l(8), l(4), cb0[0].y
itof o0.z, r0.x
bfi r0.x, l(8), l(8), cb0[0].y, cb0[0].x
utof o0.w, r0.x
ret
