ps_5_0
dcl_globalFlags refactoringAllowed
dcl_immediateConstantBuffer { { INF, 0, 0, 0 }, { -INF, 0, 0, 0 }, { NaN, 0, 0, 0 } }
dcl_input_ps constant v0.x
dcl_input_ps linear v1.x
dcl_output o0
dcl_temps 1
udiv null, r0.x, v0.x, l(3)
and r0.y, l(2143289344), icb[r0.x].x
ieq r0.y, r0.y, l(2139095040)
and r0.y, r0.y, l(1)
itof o0.x, r0.y
mov o0.w, l(1)
ne r0.y, icb[r0.x].x, icb[r0.x].x
add o0.z, v1.x, icb[r0.x].x
and r0.x, r0.y, l(1)
itof o0.y, r0.x
ret
