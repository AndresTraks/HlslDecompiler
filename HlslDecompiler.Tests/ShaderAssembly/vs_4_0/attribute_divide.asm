vs_4_0
dcl_constantbuffer cb0[5], immediateIndexed
dcl_input v0.xyz
dcl_input v1.x
dcl_output_siv o0, position
dcl_output o1.xy
dcl_temps 3
mov r0.xyz, v0.xyz
mov r0.w, l(1)
dp4 o0.x, r0, cb0[0]
dp4 o0.y, r0, cb0[1]
dp4 o0.z, r0, cb0[2]
dp4 o0.w, r0, cb0[3]
utof r0.x, cb0[4].x
udiv r1.x, r2.x, v1.x, cb0[4].x
utof r2.x, r2.x
utof r2.y, r1.x
div o1.xy, r2.xy, r0.xx
ret
