ps_4_0
dcl_constantbuffer CB0[2], immediateIndexed
dcl_input_ps linear v0.xy
dcl_output o0
dcl_output o1
dcl_temps 1
lt r0.x, v0.x, cb0[1].x
if_nz r0.x
mov o0, l(1, 0, 0, 1)
mov o1, cb0[0]
ret
endif
mul o0, v0.y, cb0[0]
mov o1.xy, v0.xy
mov o1.zw, l(0, 0, 0, 1)
ret
