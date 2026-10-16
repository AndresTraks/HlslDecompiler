ps_5_0
dcl_globalFlags refactoringAllowed
dcl_immediateConstantBuffer { { 0.5, 0, 0, 0 }, { 0.000000, 0, 0, 0 }, { NaN, 0, 0, 0 }, { 2, 0, 0, 0 }, { 0.25, 0, 0, 0 }, { 0.000000, 0, 0, 0 }, { 0.000000, 0, 0, 0 }, { 4, 0, 0, 0 }, { 0.125, 0, 0, 0 }, { 0.000000, 0, 0, 0 }, { NaN, 0, 0, 0 }, { 8, 0, 0, 0 } }
dcl_input_ps constant v0.x
dcl_input_ps linear v1.xy
dcl_output o0
dcl_temps 1
udiv null, r0.x, v0.x, l(3)
ishl r0.x, r0.x, l(2)
and r0.y, v0.x, icb[r0.x + 1].x
utof o0.y, r0.y
mul o0.x, v1.x, icb[r0.x].x
itof o0.z, icb[r0.x + 2].x
add o0.w, v1.y, icb[r0.x + 3].x
ret
