gs_4_0
dcl_constantbuffer cb0[1], immediateIndexed
dcl_input_siv v[1][0], position
dcl_temps 2
dcl_inputprimitive point
dcl_outputtopology pointlist
dcl_output_siv o0, position
dcl_output o1
dcl_maxout 3
add r0, cb0[0], v[0][0]
mov o0, r0
mov o1, l(0, 0, 0, 0)
emit
mul r1, r0, l(3, 3, 3, 3)
sqrt r0, r0
mov o0, r1
add r1, r0, v[0][0]
mov o1, r1
emit
mul r1, r0, r0
mul r0, r0, cb0[0]
mov o0, r1
mov o1, r0
emit
ret
