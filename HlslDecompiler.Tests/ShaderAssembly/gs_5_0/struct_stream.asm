gs_5_0
dcl_globalFlags refactoringAllowed
dcl_input_siv v[1][0], position
dcl_input v[1][1].xy
dcl_input v[1][2].x
dcl_temps 2
dcl_inputprimitive point
dcl_stream m0
dcl_outputtopology trianglestrip
dcl_output_siv o0, position
dcl_output o1.xy
dcl_output o2.x
dcl_maxout 4
mov o0, v[0][0]
mov o1.xy, l(0, 0, 0, 0)
ishl r0.x, v[0][2].x, l(2)
mov o2.x, r0.x
emit_stream m0
add r0, l(0.100000001, 0, 0, 0.100000001), v[0][0].xyxy
mov o0.xy, r0.xy
mov o0.zw, v[0][0].zw
mov o1.xy, l(1, 0, 0, 0)
bfi r1.xyz, l(30, 30, 30, 0), l(2, 2, 2, 0), v[0][2].xxx, l(1, 2, 3, 0)
mov o2.x, r1.x
emit_stream m0
mov o0.xy, r0.zw
mov o0.zw, v[0][0].zw
mov o1.xy, l(0, 1, 0, 0)
mov o2.x, r1.y
emit_stream m0
add r0.xy, l(0.100000001, 0.100000001, 0, 0), v[0][0].xy
mov o0.xy, r0.xy
mov o0.zw, v[0][0].zw
mov o1.xy, l(1, 1, 0, 0)
mov o2.x, r1.z
emit_stream m0
ret
