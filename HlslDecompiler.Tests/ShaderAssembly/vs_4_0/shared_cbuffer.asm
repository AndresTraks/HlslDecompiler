vs_4_0
dcl_constantbuffer cb0[6], immediateIndexed
dcl_input v0.xyz
dcl_input v1.xy
dcl_output_siv o0, position
dcl_temps 1
mul r0.xyz, v1.yyy, cb0[5].xyz
mad r0.xyz, cb0[4].xyz, v1.xxx, r0.xyz
mad r0.xyz, r0.xyz, cb0[5].www, v0.xyz
mov r0.w, l(1)
dp4 o0.x, r0, cb0[0]
dp4 o0.y, r0, cb0[1]
dp4 o0.z, r0, cb0[2]
dp4 o0.w, r0, cb0[3]
ret
