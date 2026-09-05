vs_4_0
dcl_constantbuffer cb0[20], immediateIndexed
dcl_input v0
dcl_input v1.xy
dcl_output_siv o0, position
dcl_temps 2
dp4 r0.x, v0, cb0[4]
dp4 r0.y, v0, cb0[5]
dp4 r0.z, v0, cb0[6]
dp4 r0.w, v0, cb0[7]
mul r0, r0, v1.y
dp4 r1.x, v0, cb0[0]
dp4 r1.y, v0, cb0[1]
dp4 r1.z, v0, cb0[2]
dp4 r1.w, v0, cb0[3]
mad r0, r1, v1.x, r0
dp4 o0.x, r0, cb0[16]
dp4 o0.y, r0, cb0[17]
dp4 o0.z, r0, cb0[18]
dp4 o0.w, r0, cb0[19]
ret
