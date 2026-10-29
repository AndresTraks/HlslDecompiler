gs_5_0
dcl_globalFlags refactoringAllowed
dcl_input_siv v[3][0], position
dcl_input v[3][1]
dcl_temps 2
dcl_inputprimitive triangle
dcl_stream m0
dcl_outputtopology trianglestrip
dcl_output_siv o0, position
dcl_output o1
dcl_maxout 8
mov r0.x, l(0)
loop
ige r0.y, r0.x, l(3)
breakc_nz r0.y
itof r0.y, r0.x
mov r0.z, l(0)
loop
ige r0.w, r0.z, l(2)
breakc_nz r0.w
add r1, r0.y, v[r0.z][0]
ineg r0.w, r0.z
mov o0, r1
mov o1, v[r0.w + 2][1]
emit_stream m0
iadd r0.z, r0.z, l(1)
endloop
ieq r0.y, r0.x, l(1)
if_nz r0.y
cut_stream m0
endif
iadd r0.x, r0.x, l(1)
endloop
mov o0, v[0][0]
mov o1, v[0][1]
emit_stream m0
cut_stream m0
ret
