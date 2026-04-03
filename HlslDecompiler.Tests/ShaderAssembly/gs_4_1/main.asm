gs_4_1
dcl_globalFlags refactoringAllowed
dcl_input_siv v[3][0], position
dcl_input v[3][1]
dcl_input v[3][2]
dcl_input v[3][3].x
dcl_inputprimitive triangle
dcl_outputtopology trianglestrip
dcl_output o0
dcl_output o1
dcl_output o2
dcl_output o3.x
dcl_maxout 6
cut
mov o0, v[0][0]
mov o1, v[0][1]
mov o2, v[0][2]
mov o3.x, v[0][3].x
emit
mov o0, v[1][0]
mov o1, v[1][1]
mov o2, v[1][2]
mov o3.x, v[1][3].x
emit
mov o0, l(0, 0, 0, 0)
mov o1, v[2][0]
mov o2, l(0, 0, 0, 0)
mov o3.x, l(0)
emit
ret
