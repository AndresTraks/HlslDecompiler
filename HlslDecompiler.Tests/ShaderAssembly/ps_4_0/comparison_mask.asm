ps_4_0
dcl_input_ps linear v0
dcl_output o0
dcl_temps 2
ge r0, v0, l(0.5, 0.5, 0.5, 0.5)
and r0, r0, l(1, 1, 1, 1)
ge r1, l(0.25, 0.25, 0.25, 0.25), v0
and r1, r1, l(8, 8, 8, 8)
mad o0, r0, l(2, 2, 2, 2), r1
ret
