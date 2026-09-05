ps_4_0
dcl_constantbuffer cb0[1], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 1
lt r0.x, cb0[0].x, v0.x
if_nz r0.x
mov o0, l(0, 0, 0, 0)
ret
endif
mov o0, v0
ret
