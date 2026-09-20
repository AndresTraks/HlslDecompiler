gs_4_0
dcl_constantbuffer CB0[6], immediateIndexed
dcl_input v[1][0].xyz
dcl_input v[1][1]
dcl_temps 3
dcl_inputprimitive point
dcl_outputtopology trianglestrip
dcl_output_siv o0, position
dcl_output o1.xy
dcl_output o2
dcl_maxout 4
mad r0.xyz, -cb0[4].xyz, cb0[5].www, v[0][0].xyz
mad r1.xyz, -cb0[5].xyz, cb0[5].www, r0.xyz
mad r0.xyz, cb0[5].xyz, cb0[5].www, r0.xyz
mov r1.w, l(1)
dp4 r2.x, r1, cb0[0]
mov o0.x, r2.x
dp4 r2.x, r1, cb0[1]
mov o0.y, r2.x
dp4 r2.x, r1, cb0[2]
dp4 r1.x, r1, cb0[3]
mov o0.z, r2.x
mov o0.w, r1.x
mov o1.xy, l(0, 1, 0, 0)
mov o2, v[0][1]
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
mov o1.xy, l(0, 0, 0, 0)
mov o2, v[0][1]
emit
mad r0.xyz, cb0[4].xyz, cb0[5].www, v[0][0].xyz
mad r1.xyz, -cb0[5].xyz, cb0[5].www, r0.xyz
mad r0.xyz, cb0[5].xyz, cb0[5].www, r0.xyz
mov r1.w, l(1)
dp4 r2.x, r1, cb0[0]
mov o0.x, r2.x
dp4 r2.x, r1, cb0[1]
mov o0.y, r2.x
dp4 r2.x, r1, cb0[2]
dp4 r1.x, r1, cb0[3]
mov o0.z, r2.x
mov o0.w, r1.x
mov o1.xy, l(1, 1, 0, 0)
mov o2, v[0][1]
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
mov o1.xy, l(1, 0, 0, 0)
mov o2, v[0][1]
emit
ret
