ps_4_0
dcl_constantbuffer cb0[2], immediateIndexed
dcl_input_ps_sgv constant v0.x, is_front_face
dcl_output o0
movc o0, v0.x, cb0[0], cb0[1]
ret
