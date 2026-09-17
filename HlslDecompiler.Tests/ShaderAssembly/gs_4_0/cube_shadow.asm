gs_4_0
dcl_constantbuffer cb0[24], immediateIndexed
dcl_input v[3][0]
dcl_temps 2
dcl_inputprimitive triangle
dcl_outputtopology trianglestrip
dcl_output_siv o0, position
dcl_output o1.xyz
dcl_output_siv o2.x, render_target_array_index
dcl_maxout 18
mov r0.x, l(0)
loop
ige r0.y, r0.x, l(6)
breakc_nz r0.y
ishl r0.y, r0.x, l(2)
mov r0.z, l(0)
loop
ige r0.w, r0.z, l(3)
breakc_nz r0.w
dp4 r0.w, v[r0.z][0], cb0[0]
dp4 r1.x, v[r0.z][0], cb0[0]
dp4 r1.y, v[r0.z][0], cb0[0]
dp4 r1.z, v[r0.z][0], cb0[0]
mov o0.x, r0.w
mov o0.y, r1.x
mov o0.z, r1.y
mov o0.w, r1.z
mov o1.xyz, v[r0.z][0].xyz
mov o2.x, r0.x
emit
iadd r0.z, r0.z, l(1)
endloop
cut
iadd r0.x, r0.x, l(1)
endloop
ret
