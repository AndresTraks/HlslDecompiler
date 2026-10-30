ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[9], dynamicIndexed
dcl_output o0
dcl_temps 1
ishr r0.x, cb0[0].x, l(2)
iadd r0.x, r0.x, l(1)
and r0.y, cb0[0].x, l(3)
add o0, cb0[r0.x + 1], cb0[r0.y + 3]
ret
