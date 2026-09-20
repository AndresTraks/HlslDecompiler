gs_4_0
dcl_input_siv v[3][0], position
dcl_input vPrim
dcl_temps 2
dcl_inputprimitive triangle
dcl_outputtopology trianglestrip
dcl_output_siv o0, position
dcl_output o1
dcl_output_siv o2.x, rendertarget_array_index
dcl_maxout 3
and r0.xyz, vPrim, l(3, 1, 2, 0)
movc r0.yz, r0.yz, l(0, 1, 1, 0), l(0, 0, 0, 0)
itof r1.xy, r0.yz
mov r1.zw, l(0, 0, 0, 1)
mov r0.y, l(0)
loop
ige r0.z, r0.y, l(3)
breakc_nz r0.z
mov o0, v[r0.y][0]
mov o1, r1
mov o2.x, r0.x
emit
iadd r0.y, r0.y, l(1)
endloop
ret
