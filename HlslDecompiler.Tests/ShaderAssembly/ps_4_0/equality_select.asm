ps_4_0
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 3
ne r0.x, v0.y, cb0[0].z
mul r1, v0, cb0[0].y
eq r0.yz, v0.xz, cb0[0].xw
movc r1, r0.y, r1, v0
movc r0.y, r0.z, cb0[0].x, cb0[0].y
add r2, r1, cb0[0].w
movc r1, r0.x, r2, r1
mul o0, r0.y, r1
ret
