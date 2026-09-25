gs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[24], dynamicIndexed
dcl_input_siv v[3][0], position
dcl_input v[3][1].xyz
dcl_input vGSInstanceID
dcl_temps 1
dcl_gsinstances 6
dcl_inputprimitive triangle
dcl_stream m0
dcl_outputtopology trianglestrip
dcl_output_siv o0, position
dcl_output o1.xyz
dcl_output_siv o2.x, rendertarget_array_index
dcl_maxout 3
ishl r0.x, vGSInstanceID.x, l(2)
dp4 r0.y, v[0][0], cb0[r0.x]
mov o0.x, r0.y
dp4 r0.y, v[0][0], cb0[r0.x + 1]
mov o0.y, r0.y
dp4 r0.y, v[0][0], cb0[r0.x + 2]
mov o0.z, r0.y
dp4 r0.y, v[0][0], cb0[r0.x + 3]
mov o0.w, r0.y
mov o1.xyz, v[0][1].xyz
mov o2.x, vGSInstanceID.x
emit_stream m0
dp4 r0.y, v[1][0], cb0[r0.x]
mov o0.x, r0.y
dp4 r0.y, v[1][0], cb0[r0.x + 1]
mov o0.y, r0.y
dp4 r0.y, v[1][0], cb0[r0.x + 2]
mov o0.z, r0.y
dp4 r0.y, v[1][0], cb0[r0.x + 3]
mov o0.w, r0.y
mov o1.xyz, v[1][1].xyz
mov o2.x, vGSInstanceID.x
emit_stream m0
dp4 r0.y, v[2][0], cb0[r0.x]
mov o0.x, r0.y
dp4 r0.y, v[2][0], cb0[r0.x + 1]
mov o0.y, r0.y
dp4 r0.y, v[2][0], cb0[r0.x + 2]
dp4 r0.x, v[2][0], cb0[r0.x + 3]
mov o0.z, r0.y
mov o0.w, r0.x
mov o1.xyz, v[2][1].xyz
mov o2.x, vGSInstanceID.x
emit_stream m0
ret
