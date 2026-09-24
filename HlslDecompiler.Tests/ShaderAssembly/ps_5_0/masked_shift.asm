ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_output o0
dcl_temps 1
bfi r0.xyz, l(3, 8, 4, 0), l(2, 8, 4, 0), cb0[0].xyz, l(0, 0, 0, 0)
utof o0.xyz, r0.xyz
utof o0.w, cb0[0].w
ret
