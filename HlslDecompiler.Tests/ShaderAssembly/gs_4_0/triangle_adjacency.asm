gs_4_0
dcl_input_siv v[6][0], position
dcl_temps 1
dcl_inputprimitive triangleadj
dcl_outputtopology trianglestrip
dcl_output_siv o0, position
dcl_maxout 3
mov r0.x, l(0)
loop
ige r0.y, r0.x, l(6)
breakc_nz r0.y
mov o0, v[r0.x][0]
emit
iadd r0.x, r0.x, l(2)
endloop
ret
