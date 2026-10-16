ps_5_0
dcl_globalFlags refactoringAllowed
dcl_immediateConstantBuffer { { 0.000000, 0, 0, 0 }, { 0, 0, 0, 0 }, { 0.000000, 0, 0, 0 }, { 0.000000, 0, 0, 0 }, { 0.000000, 0, 0, 0 }, { 0.000000, 0, 0, 0 }, { 0.000000, 0, 0, 0 }, { 0.000000, 0, 0, 0 }, { 1, 0, 0, 1 }, { 0, 1, 0, 1 }, { 0, 0, 1, 1 }, { 1, 1, 0, 1 }, { 1, 0, 1, 1 }, { 0, 1, 1, 1 }, { 1, 1, 1, 1 }, { 0, 0, 0, 1 } }
dcl_input_ps constant v0.x
dcl_output o0
dcl_temps 2
iadd r0.x, v0.x, l(1)
and r0.x, r0.x, l(7)
itof r0.x, icb[r0.x].x
and r0.y, v0.x, l(7)
mov r0.y, icb[r0.y].x
mov r1.xyz, icb[r0.y + 8].xyz
mov r1.w, l(1)
mul o0, r0.x, r1
ret
