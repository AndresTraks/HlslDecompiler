gs_5_0
dcl_globalFlags refactoringAllowed
dcl_input v[3][0]
dcl_input v[3][1].x
dcl_temps 1
dcl_inputprimitive triangle
dcl_stream m0
dcl_outputtopology trianglestrip
dcl_output_siv o0, position
dcl_output o1.x
dcl_maxout 3
mov r0.x, l(0)
loop
ige r0.y, r0.x, l(3)
breakc_nz r0.y
lt r0.y, l(0), v[r0.x][0].x
movc r0.y, r0.y, v[r0.x][1].x, v[r0.x][0].y
mov o0, v[r0.x][0]
mov o1.x, r0.y
emit_stream m0
iadd r0.x, r0.x, l(1)
endloop
ret
