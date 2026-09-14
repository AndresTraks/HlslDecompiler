gs_4_0
dcl_input_siv v[4][0], position
dcl_temps 1
dcl_inputprimitive lineadj
dcl_outputtopology linestrip
dcl_output_siv o0, position
dcl_output_siv o1.x, viewport_array_index
dcl_maxout 4
mov o0, v[1][0]
mov o1.x, l(0)
emit
mov o0, v[2][0]
mov o1.x, l(0)
emit
cut
add r0, v[1][0], v[0][0]
mul r0, r0, l(0.5, 0.5, 0.5, 0.5)
mov o0, r0
mov o1.x, l(1)
emit
add r0, v[3][0], v[2][0]
mul r0, r0, l(0.5, 0.5, 0.5, 0.5)
mov o0, r0
mov o1.x, l(1)
emit
ret
