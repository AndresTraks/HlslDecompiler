gs_4_1
dcl_globalFlags refactoringAllowed
dcl_input_siv v[1][0], position
dcl_temps 3
dcl_inputprimitive point
dcl_outputtopology trianglestrip
dcl_output_siv o0, position
dcl_output o1.xy
dcl_maxout 4
mov r0.zw, l(0, 0, 0, 0)
mov r1.x, l(0)
loop
ige r1.y, r1.x, l(4)
breakc_nz r1.y
itof r0.xy, r1.xx
add r2, r0, v[0][0]
mov o0, r2
mov o1.xy, r0.yy
emit
iadd r1.x, r1.x, l(1)
endloop
cut
ret
