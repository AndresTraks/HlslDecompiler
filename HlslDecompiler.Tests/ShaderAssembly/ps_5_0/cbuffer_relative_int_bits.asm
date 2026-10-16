ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[17], dynamicIndexed
dcl_input_ps constant v1.x
dcl_output o0
dcl_temps 2
iadd r0.x, v1.x, cb0[16].x
and r0.x, r0.x, l(7)
xor r0.y, r0.x, l(1)
itof r0.zw, cb0[r0.x + 8].xy
mad r1, cb0[r0.x], r0.z, cb0[r0.y + 8]
add o0, r0.w, r1
ret
