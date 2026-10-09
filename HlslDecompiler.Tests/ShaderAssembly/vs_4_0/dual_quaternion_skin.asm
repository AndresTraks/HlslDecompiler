vs_4_0
dcl_constantbuffer CB0[68], dynamicIndexed
dcl_input v0.xyz
dcl_input v1.xy
dcl_input v2.xy
dcl_output_siv o0, position
dcl_temps 4
mov r0.x, v1.y
mul r1, v2.y, cb0[r0.x + 32]
mul r0, v2.y, cb0[r0.x]
mov r2.x, v1.x
mad r1, cb0[r2.x + 32], v2.x, r1
mad r0, cb0[r2.x], v2.x, r0
dp4 r2.x, r0, r0
sqrt r2.x, r2.x
div r1, r1, r2.x
div r0, r0, r2.x
mul r2.xyz, r0.xyz, r1.www
mad r2.xyz, r0.www, r1.xyz, -r2.xyz
mul r3.xyz, r1.yzx, r0.zxy
mad r1.xyz, r0.yzx, r1.zxy, -r3.xyz
add r1.xyz, r1.xyz, r2.xyz
mul r2.xyz, r0.xyz, v0.zxy
mad r2.xyz, r0.zxy, v0.xyz, -r2.xyz
mad r2.xyz, r0.www, v0.yzx, r2.xyz
mul r3.xyz, r0.zxy, r2.xyz
mad r0.xyz, r0.yzx, r2.yzx, -r3.xyz
mad r0.xyz, r0.xyz, l(2, 2, 2, 0), v0.xyz
mad r0.xyz, r1.xyz, l(2, 2, 2, 0), r0.xyz
mov r0.w, l(1)
dp4 o0.x, r0, cb0[64]
dp4 o0.y, r0, cb0[65]
dp4 o0.z, r0, cb0[66]
dp4 o0.w, r0, cb0[67]
ret
