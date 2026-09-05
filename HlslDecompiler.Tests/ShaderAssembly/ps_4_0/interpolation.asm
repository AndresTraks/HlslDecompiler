ps_4_0
dcl_constantbuffer cb0[1], immediateIndexed
dcl_input_ps constant v0
dcl_input_ps linear centroid v1
dcl_input_ps linear noperspective v2
dcl_output o0
dcl_temps 1
mad r0, v0, cb0[0], v1
add o0, r0, v2
ret
