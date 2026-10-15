gs_4_0
dcl_input_siv v[1][0], position
dcl_input v[1][1].x
dcl_input v[1][2].x
dcl_temps 2
dcl_inputprimitive point
dcl_outputtopology trianglestrip
dcl_output_siv o0, position
dcl_output o1.x
dcl_output o2.x
dcl_maxout 4
mov r0.x, l(0)
loop
uge r0.y, r0.x, l(4)
breakc_nz r0.y
and r0.y, r0.x, l(1)
ushr r0.z, r0.x, l(1)
utof r1.xy, r0.yz
mad r0.yz, r1.xy, l(0, 0.100000001, 0.100000001, 0), v[0][0].xy
ishl r0.w, r0.x, l(8)
or r0.w, r0.w, v[0][1].x
utof r1.x, r0.x
mul r1.x, r1.x, v[0][1].x
mov o0.xy, r0.yz
mov o0.zw, v[0][0].zw
mov o1.x, r0.w
mov o2.x, r1.x
emit
iadd r0.x, r0.x, l(1)
endloop
ret
