vs_4_0
dcl_constantbuffer CB0[5], immediateIndexed
dcl_input v0
dcl_input v1
dcl_output_siv o0, position
dcl_output o1
dcl_temps 2
dp4 o0.x, v0, cb0[0]
dp4 o0.y, v0, cb0[1]
dp4 o0.z, v0, cb0[2]
dp4 o0.w, v0, cb0[3]
lt r0, cb0[4], v1
and r1.xy, r0.zw, r0.xy
or r0.xy, r0.zw, r0.xy
or r0.x, r0.y, r0.x
and r0.y, r1.y, r1.x
mul r1, v1, l(0.5, 0.5, 0.5, 0.5)
movc r1, r0.y, v1, r1
movc o1, r0.x, r1, l(0, 0, 0, 1)
ret
