vs_4_0
dcl_constantbuffer cb0[36], immediateIndexed
dcl_input v0
dcl_input_sgv v1.x, instance_id
dcl_output_siv o0, position
dcl_temps 2
ishl r0.x, v1.x, l(2)
dp4 r1.x, v0, cb0[0]
dp4 r1.y, v0, cb0[0]
dp4 r1.z, v0, cb0[0]
dp4 r1.w, v0, cb0[0]
dp4 o0.x, r1, cb0[0]
dp4 o0.y, r1, cb0[1]
dp4 o0.z, r1, cb0[2]
dp4 o0.w, r1, cb0[3]
ret
