vs_4_0
dcl_constantbuffer CB0[52], dynamicIndexed
dcl_input v0
dcl_input_sgv v1.x, instance_id
dcl_output_siv o0, position
dcl_output o1
dcl_temps 3
imul null, r0.x, v1.x, l(6)
mul r1, v0, cb0[r0.x + 5].x
dp4 r2.x, r1, cb0[r0.x]
dp4 r2.y, r1, cb0[r0.x + 1]
dp4 r2.z, r1, cb0[r0.x + 2]
dp4 r2.w, r1, cb0[r0.x + 3]
mov o1, cb0[r0.x + 4]
dp4 o0.x, r2, cb0[48]
dp4 o0.y, r2, cb0[49]
dp4 o0.z, r2, cb0[50]
dp4 o0.w, r2, cb0[51]
ret
