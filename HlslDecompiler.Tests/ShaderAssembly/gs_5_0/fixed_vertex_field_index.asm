gs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input_siv v[1][0], position
dcl_input v[1][1]
dcl_input v[1][2]
dcl_input v[1][3]
dcl_temps 1
dcl_indexrange v[1][1] 3
dcl_inputprimitive point
dcl_stream m0
dcl_outputtopology pointlist
dcl_output_siv o0, position
dcl_maxout 1
and r0.x, cb0[0].x, l(-2147483648)
imax r0.y, cb0[0].x, -cb0[0].x
udiv null, r0.y, r0.y, l(3)
ineg r0.z, r0.y
movc r0.x, r0.x, r0.z, r0.y
add r0, v[0][0], v[0][r0.x + 1]
mov o0, r0
emit_stream m0
ret
