ps_4_0
dcl_constantbuffer cb0[4], immediateIndexed
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 1
lt r0.x, cb0[3].x, v0.x
mov o0, cb0[0]
RetC
lt r0.x, cb0[3].x, v0.y
if_nz r0.x
mov o0, cb0[1]
ret
endif
mov o0, cb0[2]
ret
