gs_4_1
dcl_globalFlags refactoringAllowed
dcl_input_siv v[3][0], position
dcl_input v[3][1]
dcl_inputprimitive triangle
dcl_outputtopology linestrip
dcl_output_siv o0, position
dcl_output o1
dcl_maxout 6
cut
mov o0, v[0][0]
mov o1, v[0][1]
emit
mov o0, v[1][0]
mov o1, v[1][1]
emit
ret
