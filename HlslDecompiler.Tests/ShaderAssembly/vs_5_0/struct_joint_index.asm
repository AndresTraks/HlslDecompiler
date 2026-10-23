vs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[69], dynamicIndexed
dcl_input v0.xyz
dcl_input v1
dcl_input v2
dcl_output_siv o0, position
dcl_temps 4
mov r0.xyz, v0.xyz
mov r0.w, l(1)
ishl r1, v1, l(2, 2, 2, 2)
dp4 r2.x, cb0[r1.y], r0
dp4 r2.y, cb0[r1.y + 1], r0
dp4 r2.z, cb0[r1.y + 2], r0
mul r2.xyz, r2.xyz, v2.yyy
mul r2.xyz, r2.xyz, cb0[r1.y + 3].xxx
dp4 r3.x, cb0[r1.x], r0
dp4 r3.y, cb0[r1.x + 1], r0
dp4 r3.z, cb0[r1.x + 2], r0
mul r3.xyz, r3.xyz, v2.xxx
mad r2.xyz, r3.xyz, cb0[r1.x + 3].xxx, r2.xyz
dp4 r3.x, cb0[r1.z], r0
dp4 r3.y, cb0[r1.z + 1], r0
dp4 r3.z, cb0[r1.z + 2], r0
mul r3.xyz, r3.xyz, v2.zzz
mad r1.xyz, r3.xyz, cb0[r1.z + 3].xxx, r2.xyz
dp4 r2.x, cb0[r1.w], r0
dp4 r2.y, cb0[r1.w + 1], r0
dp4 r2.z, cb0[r1.w + 2], r0
mul r0.xyz, r2.xyz, v2.www
mad r0.xyz, r0.xyz, cb0[r1.w + 3].xxx, r1.xyz
mov r0.w, l(1)
dp4 r1.x, r0, cb0[65]
dp4 r1.y, r0, cb0[66]
dp4 r1.z, r0, cb0[67]
dp4 r1.w, r0, cb0[68]
mul o0, r1, cb0[64].w
ret
