gs_5_0
dcl_globalFlags refactoringAllowed
dcl_input_siv v[3][0], position
dcl_input v[3][1].x
dcl_input v[3][2].x
dcl_inputprimitive triangle
dcl_stream m0
dcl_outputtopology trianglestrip
dcl_output_siv o0, position
dcl_output o1.xy
dcl_output o2.xy
dcl_maxout 3
mov o0, v[0][0]
mov o1.x, v[0][1].x
mov o1.y, v[0][2].x
mov o2.x, v[0][2].x
mov o2.y, l(0)
emit_stream m0
mov o0, v[1][0]
mov o1.x, v[1][1].x
mov o1.y, v[1][2].x
mov o2.x, v[1][2].x
mov o2.y, l(1)
emit_stream m0
mov o0, v[2][0]
mov o1.x, v[2][1].x
mov o1.y, v[2][2].x
mov o2.x, v[2][2].x
mov o2.y, l(2)
emit_stream m0
ret
