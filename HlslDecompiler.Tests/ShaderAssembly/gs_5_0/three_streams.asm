gs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[4], immediateIndexed
dcl_input_siv v[3][0], position
dcl_input v[3][1].xyz
dcl_temps 1
dcl_inputprimitive triangle
dcl_stream m0
dcl_outputtopology pointlist
dcl_output_siv o0, position
dcl_output o1.x
dcl_stream m1
dcl_outputtopology pointlist
dcl_output_siv o0, position
dcl_output o1.xyz
dcl_stream m2
dcl_outputtopology pointlist
dcl_output_siv o0, position
dcl_output o1.x
dcl_maxout 6
mov o0, v[0][0]
mov o1.x, v[0][1].x
emit_stream m0
add r0, cb0[0], v[0][0]
mov o0, r0
mov o1.xyz, v[0][1].xyz
emit_stream m1
add r0, cb0[1], v[1][0]
mov o0, r0
mov o1.xyz, v[1][1].xyz
emit_stream m1
lt r0.x, cb0[3].x, v[2][1].z
if_nz r0.x
iadd r0.x, cb0[3].y, l(1)
mov o0, v[2][0]
mov o1.x, r0.x
emit_stream m2
endif
ret
