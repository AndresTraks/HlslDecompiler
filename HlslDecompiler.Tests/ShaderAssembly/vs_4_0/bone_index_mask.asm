vs_4_0
dcl_constantbuffer CB0[32], dynamicIndexed
dcl_input v0
dcl_input v1
dcl_input v2.xyz
dcl_output_siv o0, position
dcl_temps 3
ishl r0.xy, v1.xy, l(2)
and r0.y, r0.y, l(28)
dp4 r1.x, v0, cb0[r0.y]
dp4 r1.y, v0, cb0[r0.y + 1]
dp4 r1.z, v0, cb0[r0.y + 2]
dp4 r1.w, v0, cb0[r0.y + 3]
mul r1, r1, v2.y
dp4 r2.x, v0, cb0[r0.x]
dp4 r2.y, v0, cb0[r0.x + 1]
dp4 r2.z, v0, cb0[r0.x + 2]
dp4 r2.w, v0, cb0[r0.x + 3]
mad r0, r2, v2.x, r1
iadd r1.x, v1.w, v1.z
imax r1.y, r1.x, -r1.x
and r1.xy, r1.xy, l(-2147483648, 7, 0, 0)
ineg r1.z, r1.y
movc r1.x, r1.x, r1.z, r1.y
ishl r1.x, r1.x, l(2)
dp4 r2.x, v0, cb0[r1.x]
dp4 r2.y, v0, cb0[r1.x + 1]
dp4 r2.z, v0, cb0[r1.x + 2]
dp4 r2.w, v0, cb0[r1.x + 3]
mad o0, r2, v2.z, r0
ret
