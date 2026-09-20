vs_4_0
dcl_constantbuffer CB0[6], immediateIndexed
dcl_input v0
dcl_output_siv o0, position
dcl_output_siv o1.x, clip_distance
dcl_output_siv o1.y, cull_distance
dp4 o0.x, v0, cb0[0]
dp4 o0.y, v0, cb0[1]
dp4 o0.z, v0, cb0[2]
dp4 o0.w, v0, cb0[3]
dp4 o1.x, v0, cb0[4]
dp4 o1.y, v0, cb0[5]
ret
