ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_output o0
dcl_temps 1
bfi r0.xy, l(8, 8, 0, 0), l(16, 8, 0, 0), cb0[0].zy, l(0, 0, 0, 0)
bfi r0.y, l(8), l(0), cb0[0].x, r0.y
iadd r0.x, r0.x, r0.y
bfi r0.x, l(8), l(24), cb0[0].w, r0.x
and r0.y, r0.x, l(255)
ubfe r0.xz, l(8, 0, 8, 0), l(8, 0, 16, 0), r0.xx
utof o0.xyz, r0.yxz
and r0.x, cb0[0].w, l(255)
utof o0.w, r0.x
ret
