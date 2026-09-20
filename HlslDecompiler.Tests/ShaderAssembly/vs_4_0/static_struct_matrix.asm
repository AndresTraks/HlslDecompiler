vs_4_0
dcl_constantbuffer CB0[14], immediateIndexed
dcl_input v0
dcl_output_siv o0, position
dcl_output o1
dcl_output o2
dp4 o0.x, v0, cb0[10]
dp4 o0.y, v0, cb0[11]
dp4 o0.z, v0, cb0[12]
dp4 o0.w, v0, cb0[13]
dp4 o1.x, v0, cb0[5]
dp4 o1.y, v0, cb0[6]
dp4 o1.z, v0, cb0[7]
dp4 o1.w, v0, cb0[8]
add o2, cb0[4], cb0[9]
ret
