gs_5_0
dcl_globalFlags refactoringAllowed
dcl_input v[3][0]
dcl_input vPrim
dcl_temps 2
dcl_inputprimitive triangle
dcl_stream m0
dcl_outputtopology trianglestrip
dcl_output_siv o0, position
dcl_output o1.x
dcl_output o2.x
dcl_maxout 3
utof r0.x, vPrim
mov r0.y, l(0)
loop
ige r0.z, r0.y, l(3)
breakc_nz r0.z
lt r0.zw, l(0, 0, 0, 0), v[r0.y][0].xz
iadd r1.x, r0.y, vPrim
movc r0.z, r0.z, v[r0.y][0].y, r1.x
movc r0.w, r0.w, vPrim, r0.x
mov o0, v[r0.y][0]
mov o1.x, r0.z
mov o2.x, r0.w
emit_stream m0
iadd r0.y, r0.y, l(1)
endloop
ret
