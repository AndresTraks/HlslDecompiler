gs_5_0
dcl_globalFlags refactoringAllowed
dcl_input_siv v[3][0], position
dcl_input vPrim
dcl_temps 1
dcl_inputprimitive triangle
dcl_stream m0
dcl_outputtopology trianglestrip
dcl_output_siv o0, position
dcl_output o1.x
dcl_maxout 3
mov o0.xyz, v[0][0].xyz
ieq r0.x, vPrim, l(3)
movc r0.y, r0.x, l(1088421888), v[0][0].y
xor r0.z, r0.y, l(-2147483648)
ushr r0.y, r0.y, l(30)
mov o0.w, r0.z
mov o1.x, r0.y
emit_stream m0
mov o0.xyz, v[1][0].xyz
movc r0.y, r0.x, l(1088421888), v[1][0].y
movc r0.x, r0.x, l(1088421888), v[2][0].y
xor r0.z, r0.y, l(-2147483648)
ushr r0.y, r0.y, l(30)
mov o0.w, r0.z
mov o1.x, r0.y
emit_stream m0
mov o0.xyz, v[2][0].xyz
xor r0.y, r0.x, l(-2147483648)
ushr r0.x, r0.x, l(30)
mov o0.w, r0.y
mov o1.x, r0.x
emit_stream m0
ret
