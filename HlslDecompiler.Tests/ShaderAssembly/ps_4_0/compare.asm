ps_4_0
dcl_constantbuffer cb0[1], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 1
lt r0.x, v0.x, cb0[0].x
movc o0, r0.x, v0, -v0
ret
