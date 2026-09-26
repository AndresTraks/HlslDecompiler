gs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input_siv v[1][0], position
dcl_input v[1][1].xyz
dcl_temps 1
dcl_inputprimitive point
dcl_stream m0
dcl_outputtopology pointlist
dcl_output_siv o0, position
dcl_output o1.xyz
dcl_stream m1
dcl_outputtopology pointlist
dcl_output_siv o0, position
dcl_output o1.x
dcl_maxout 1
dp3 r0.x, v[0][1].xyz, v[0][1].xyz
sqrt r0.x, r0.x
lt r0.y, cb0[0].w, r0.x
if_nz r0.y
add r0.yzw, cb0[0].xyz, v[0][1].xyz
mov o0, v[0][0]
mov o1.xyz, r0.yzw
emit_stream m0
else
mov o0, v[0][0]
mov o1.x, r0.x
emit_stream m1
endif
ret
