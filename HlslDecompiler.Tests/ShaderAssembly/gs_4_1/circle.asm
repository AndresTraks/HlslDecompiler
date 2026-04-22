gs_4_1
dcl_globalFlags refactoringAllowed
dcl_input_siv v[1][0], position
dcl_input v[1][1]
dcl_temps 3
dcl_inputprimitive point
dcl_outputtopology trianglestrip
dcl_output_siv o0, position
dcl_output o1
dcl_maxout 17
mov o0, v[0][0]
mov o1, v[0][1]
emit
mov r0.zw, l(0, 0, 0.5, 0.5)
mov r1.x, l(1)
loop
ilt r1.y, l(17), r1.x
breakc_nz r1.y
itof r1.y, r1.x
mul r1.y, r1.y, l(0.392699)
sincos r2.x, r0.x, r1.y
mov r0.y, r2.x
mad r2, r0, l(0.5, 0.5, 0, 0), v[0][0]
mov o0, r2
mov o1, v[0][1]
emit
iadd r1.x, r1.x, l(1)
endloop
ret
