gs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[24], dynamicIndexed
dcl_input v[3][0].xyz
dcl_temps 3
dcl_inputprimitive triangle
dcl_stream m0
dcl_outputtopology trianglestrip
dcl_output_siv o0, position
dcl_output_siv o1.x, rendertarget_array_index
dcl_maxout 18
mov r0.w, l(1)
mov r1.x, l(0)
loop
ige r1.y, r1.x, l(6)
breakc_nz r1.y
ishl r1.y, r1.x, l(2)
mov r1.z, l(0)
loop
ige r1.w, r1.z, l(3)
breakc_nz r1.w
mov r0.xyz, v[r1.z][0].xyz
dp4 r1.w, r0, cb0[r1.y]
dp4 r2.x, r0, cb0[r1.y + 1]
dp4 r2.y, r0, cb0[r1.y + 2]
dp4 r0.x, r0, cb0[r1.y + 3]
mov o0.x, r1.w
mov o0.y, r2.x
mov o0.z, r2.y
mov o0.w, r0.x
mov o1.x, r1.x
emit_stream m0
iadd r1.z, r1.z, l(1)
endloop
cut_stream m0
iadd r1.x, r1.x, l(1)
endloop
ret
