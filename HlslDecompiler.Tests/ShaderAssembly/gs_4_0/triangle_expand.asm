gs_4_0
dcl_constantbuffer cb0[1], immediateIndexed
dcl_input_siv v[3][0], position
dcl_input v[3][1].xyz
dcl_input v[3][2].xy
dcl_temps 3
dcl_inputprimitive triangle
dcl_outputtopology trianglestrip
dcl_output_siv o0, position
dcl_output o1.xyz
dcl_output o2.xy
dcl_maxout 6
mov r0.x, l(0)
loop
ige r0.y, r0.x, l(3)
breakc_nz r0.y
mad r1, v[r0.x][0], cb0[0].x, cb0[0]
add r0.yzw, cb0[0].xyz, v[r0.x][1].xyz
dp3 r2.x, r0.yzw, r0.yzw
rsq r2.x, r2.x
mul r0.yzw, r0.yzw, r2.xxx
mul r2.xy, cb0[0].zw, v[r0.x][2].xy
mov o0, r1
mov o1.xyz, r0.yzw
mov o2.xy, r2.xy
emit
iadd r0.x, r0.x, l(1)
endloop
cut
mov r0.x, l(2)
loop
ilt r0.y, r0.x, l(0)
breakc_nz r0.y
add r1, -cb0[0], v[r0.x][0]
add r0.yz, cb0[0].xy, v[r0.x][2].xy
mov o0, r1
mov o1.xyz, -v[r0.x][1].xyz
mov o2.xy, r0.yz
emit
iadd r0.x, r0.x, l(-1)
endloop
ret
