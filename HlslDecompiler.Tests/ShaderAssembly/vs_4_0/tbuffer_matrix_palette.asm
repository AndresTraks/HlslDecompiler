vs_4_0
dcl_constantbuffer CB0[5], immediateIndexed
dcl_resource_buffer (mixed,mixed,mixed,mixed) t0
dcl_resource_buffer (float,float,float,float) t1
dcl_input v0.xyz
dcl_input v1.xy
dcl_input v2.xy
dcl_input_sgv v3.x, vertex_id
dcl_output_siv o0, position
dcl_temps 5
imul null, r0.xy, v1.xy, l(3, 3, 0, 0)
ld r1, r0.y, t0
ld r0, r0.x, t0
ld r2, v3.x, t1
mad r2.xyz, r2.xyz, cb0[4].xxx, v0.xyz
iadd r2.w, v3.x, cb0[4].y
ld r3, r2.w, t1
add r2.w, -cb0[4].x, l(1)
mad r2.xyz, r3.xyz, r2.www, r2.xyz
mov r2.w, l(1)
dp4 r1.x, r2, r1
imad r3, v1.xxyy, l(3, 3, 3, 3), l(2, 1, 2, 1)
ld r4, r3.w, t0
dp4 r1.y, r2, r4
ld r4, r3.z, t0
dp4 r1.z, r2, r4
mul r1.xyz, r1.xyz, v2.yyy
dp4 r0.x, r2, r0
ld r4, r3.y, t0
ld r3, r3.x, t0
dp4 r0.z, r2, r3
dp4 r0.y, r2, r4
mad r0.xyz, r0.xyz, v2.xxx, r1.xyz
mov r0.w, l(1)
dp4 o0.x, r0, cb0[0]
dp4 o0.y, r0, cb0[1]
dp4 o0.z, r0, cb0[2]
dp4 o0.w, r0, cb0[3]
ret
