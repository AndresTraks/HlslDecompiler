gs_4_0
dcl_constantbuffer cb0[1], immediateIndexed
dcl_input_siv v[2][0], position
dcl_temps 1
dcl_inputprimitive line
dcl_outputtopology linestrip
dcl_output_siv o0, position
dcl_output o1
dcl_maxout 4
mov o0, v[0][0]
mov o1, l(1, 0, 0, 1)
emit
mov o0, v[1][0]
mov o1, l(1, 0, 0, 1)
emit
cut
add r0, cb0[0], v[0][0]
mov o0, r0
mov o1, l(0, 1, 0, 1)
emit
add r0, cb0[0], v[1][0]
mov o0, r0
mov o1, l(0, 1, 0, 1)
emit
ret
