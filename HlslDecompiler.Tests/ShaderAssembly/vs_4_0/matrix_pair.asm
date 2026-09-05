vs_4_0
dcl_constantbuffer cb0[8], immediateIndexed
dcl_input v0
dcl_output_siv o0, position
dcl_temps 1
dp4 r0.x, v0, cb0[0]
dp4 r0.y, v0, cb0[1]
dp4 r0.z, v0, cb0[2]
dp4 r0.w, v0, cb0[3]
dp4 o0.x, r0, cb0[4]
dp4 o0.y, r0, cb0[5]
dp4 o0.z, r0, cb0[6]
dp4 o0.w, r0, cb0[7]
ret
