vs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_buffer (mixed,mixed,mixed,mixed) t0
dcl_input v0.xyz
dcl_input v1.xy
dcl_input v2.xy
dcl_output_siv o0, position
dcl_output o1
dcl_temps 5
ishl r0.xy, v1.xy, l(2, 2, 0, 0)
ld_indexable(buffer)(mixed,mixed,mixed,mixed) r1, r0.y, t0
ld_indexable(buffer)(mixed,mixed,mixed,mixed) r0, r0.x, t0
mov r2.xyz, v0.xyz
mov r2.w, l(1)
dp4 r1.x, r2, r1
bfi r3.xy, l(30, 30, 0, 0), l(2, 2, 0, 0), v1.yy, l(2, 1, 0, 0)
ld_indexable(buffer)(mixed,mixed,mixed,mixed) r4, r3.y, t0
ld_indexable(buffer)(mixed,mixed,mixed,mixed) r3, r3.x, t0
dp4 r1.z, r2, r3
dp4 r1.y, r2, r4
bfi r3, l(30, 30, 30, 30), l(2, 2, 2, 2), v1.xxxy, l(3, 2, 1, 3)
ld_indexable(buffer)(mixed,mixed,mixed,mixed) r4, r3.w, t0
dp4 r1.w, r2, r4
mul r1, r1, v2.y
dp4 r0.x, r2, r0
ld_indexable(buffer)(mixed,mixed,mixed,mixed) r4, r3.z, t0
dp4 r0.y, r2, r4
ld_indexable(buffer)(mixed,mixed,mixed,mixed) r4, r3.y, t0
ld_indexable(buffer)(mixed,mixed,mixed,mixed) r3, r3.x, t0
dp4 r0.w, r2, r3
dp4 r0.z, r2, r4
mad o0, r0, v2.x, r1
ld_indexable(buffer)(mixed,mixed,mixed,mixed) r0, l(64, 64, 64, 64), t0
mov o1, r0
ret
