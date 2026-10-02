gs_5_0
dcl_globalFlags refactoringAllowed
dcl_input_siv v[3][0], position
dcl_input vPrim
dcl_temps 1
dcl_inputprimitive triangle
dcl_stream m0
dcl_outputtopology trianglestrip
dcl_output_siv o0, position
dcl_output_sgv o1.x, primitive_id
dcl_maxout 3
mov r0.x, l(0)
loop
ige r0.y, r0.x, l(3)
breakc_nz r0.y
mov o0, v[r0.x][0]
mov o1.x, vPrim
emit_stream m0
iadd r0.x, r0.x, l(1)
endloop
ret
