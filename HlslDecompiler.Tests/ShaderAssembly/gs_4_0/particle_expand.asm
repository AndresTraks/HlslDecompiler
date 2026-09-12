gs_4_0
dcl_constantbuffer cb0[6], immediateIndexed
dcl_input_siv v[1][0], position
dcl_input v[1][1]
dcl_input v[1][2].x
dcl_temps 3
dcl_inputprimitive point
dcl_outputtopology trianglestrip
dcl_output_siv o0, position
dcl_output o1
dcl_output o2.xy
dcl_maxout 4
mul r0.xyz, cb0[5].xyz, v[0][2].xxx
mad r1.xyz, -cb0[4].xyz, v[0][2].xxx, -r0.xyz
add r1.xyz, r1.xyz, v[0][0].xyz
mov r1.w, l(1)
dp4 r0.w, r1, cb0[0]
mov o0.x, r0.w
dp4 r0.w, r1, cb0[1]
mov o0.y, r0.w
dp4 r0.w, r1, cb0[2]
dp4 r1.x, r1, cb0[3]
mov o0.z, r0.w
mov o0.w, r1.x
mov o1, v[0][1]
mov o2.xy, l(0, 0, 0, 0)
emit
mad r1.xyz, cb0[4].xyz, v[0][2].xxx, -r0.xyz
mad r0.xyz, cb0[4].xyz, v[0][2].xxx, r0.xyz
add r0.xyz, r0.xyz, v[0][0].xyz
add r1.xyz, r1.xyz, v[0][0].xyz
mov r1.w, l(1)
dp4 r2.x, r1, cb0[0]
mov o0.x, r2.x
dp4 r2.x, r1, cb0[1]
mov o0.y, r2.x
dp4 r2.x, r1, cb0[2]
dp4 r1.x, r1, cb0[3]
mov o0.z, r2.x
mov o0.w, r1.x
mov o1, v[0][1]
mov o2.xy, l(1, 0, 0, 0)
emit
mul r1.xyz, cb0[4].xyz, v[0][2].xxx
mad r1.xyz, cb0[5].xyz, v[0][2].xxx, -r1.xyz
add r1.xyz, r1.xyz, v[0][0].xyz
mov r1.w, l(1)
dp4 r2.x, r1, cb0[0]
mov o0.x, r2.x
dp4 r2.x, r1, cb0[1]
mov o0.y, r2.x
dp4 r2.x, r1, cb0[2]
dp4 r1.x, r1, cb0[3]
mov o0.z, r2.x
mov o0.w, r1.x
mov o1, v[0][1]
mov o2.xy, l(0, 1, 0, 0)
emit
mov r0.w, l(1)
dp4 r1.x, r0, cb0[0]
mov o0.x, r1.x
dp4 r1.x, r0, cb0[1]
mov o0.y, r1.x
dp4 r1.x, r0, cb0[2]
dp4 r0.x, r0, cb0[3]
mov o0.z, r1.x
mov o0.w, r0.x
mov o1, v[0][1]
mov o2.xy, l(1, 1, 0, 0)
emit
ret
