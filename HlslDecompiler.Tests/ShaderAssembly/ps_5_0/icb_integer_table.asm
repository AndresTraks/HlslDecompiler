ps_5_0
dcl_globalFlags refactoringAllowed
dcl_immediateConstantBuffer { { 1, 0.5, 0, 0 }, { 0.000000, 1.5, 0, 0 }, { 3, 2.5, 0, 0 }, { 0.000000, 3.5, 0, 0 } }
dcl_input_ps linear v0.x
dcl_input_ps constant v1.x
dcl_output o0
dcl_temps 1
and r0.x, v1.x, l(3)
and r0.y, l(1), icb[r0.x].x
movc o0.w, r0.y, l(3), icb[r0.x].y
xor r0.y, r0.x, l(1)
utof o0.y, icb[r0.y].x
mul o0.z, v0.x, icb[r0.x].y
mov o0.x, icb[r0.x].x
ret
