ps_4_0
dcl_input_ps linear v0
dcl_output o0
dcl_temps 2
div r0, l(1, 1, 1, 1), v0
mov_sat r0, r0
ge r1, l(0.5, 0.5, 0.5, 0.5), v0
And
add o0, r0, r1
ret
