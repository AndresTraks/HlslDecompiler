ps_4_0
dcl_constantbuffer cb0[1], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 1
add r0.x, v0.w, -cb0[0].x
lt r0.x, r0.x, l(0)
discard_nz r0.x
mov o0, v0
ret
