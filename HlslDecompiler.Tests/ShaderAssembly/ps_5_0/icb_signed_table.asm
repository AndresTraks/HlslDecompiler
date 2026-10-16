ps_5_0
dcl_globalFlags refactoringAllowed
dcl_immediateConstantBuffer { { NaN, 0, 0, 0 }, { NaN, 0, 0, 0 }, { 0.000000, 0, 0, 0 }, { 0.000000, 0, 0, 0 } }
dcl_input_ps constant v0.x
dcl_input_ps linear v1.x
dcl_output o0
dcl_temps 1
and r0.x, v0.x, l(3)
ishr r0.y, icb[r0.x].x, l(1)
itof o0.w, r0.y
imax r0.y, -icb[r0.x].x, icb[r0.x].x
itof r0.x, icb[r0.x].x
itof o0.y, r0.y
mul o0.z, r0.x, v1.x
mov o0.x, r0.x
ret
