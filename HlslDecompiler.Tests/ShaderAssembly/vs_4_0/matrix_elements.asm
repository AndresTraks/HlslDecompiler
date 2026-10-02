vs_4_0
dcl_constantbuffer CB0[9], immediateIndexed
dcl_input v0.xyz
dcl_input v1.xy
dcl_output_siv o0, position
dcl_output o1.xy
dcl_output o1.z
dcl_temps 2
add r0.xyz, v0.xyz, -cb0[8].xyz
mul r1.x, r0.x, cb0[0].x
mul r1.y, r0.y, cb0[1].y
mul r1.z, r0.z, cb0[2].z
add r0.xyz, r1.xyz, cb0[3].xyz
mov r0.w, l(1)
dp4 o0.x, r0, cb0[4]
dp4 o0.y, r0, cb0[5]
dp4 o0.z, r0, cb0[6]
dp4 o0.w, r0, cb0[7]
mul o1.z, cb0[2].z, cb0[3].z
mov o1.xy, v1.xy
ret
