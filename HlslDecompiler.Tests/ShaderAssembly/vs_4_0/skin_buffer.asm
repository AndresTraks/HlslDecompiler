vs_4_0
dcl_immediateConstantBuffer { { 1, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } }
dcl_constantbuffer cb0[4], immediateIndexed
dcl_resource_buffer (float,float,float,float) t0
dcl_input v0
dcl_input v1
dcl_input v2
dcl_output_siv o0, position
dcl_temps 5
mov r0.xyz, l(0, 0, 0, 0)
mov r1.x, l(0)
loop
ige r1.y, r1.x, l(3)
breakc_nz r1.y
ineg r1.y, r1.x
ult r2.xyz, r1.xxx, l(1, 2, 3, 0)
and r3.y, r1.y, r2.y
iadd r1.yz, r1.xx, l(0, -3, 1, 0)
movc r3.z, r2.y, l(0), r1.y
ieq r3.w, r2.z, l(0)
mov r3.x, r2.x
and r2, r3, v2
or r1.yw, r2.yw, r2.xz
or r1.y, r1.w, r1.y
imul null, r1.w, r1.y, l(3)
ld r2, r1.w, t0
imad r1.yw, r1.yy, l(0, 3, 0, 3), l(0, 1, 0, 2)
ld r3, r1.y, t0
ld r4, r1.w, t0
dp4 r2.x, r2, v0
dp4 r2.y, r3, v0
dp4 r2.z, r4, v0
dp4 r1.y, v1, icb[r1.x]
mad r0.xyz, r2.xyz, r1.yyy, r0.xyz
mov r1.x, r1.z
endloop
mov r0.w, l(1)
dp4 o0.x, r0, cb0[0]
dp4 o0.y, r0, cb0[1]
dp4 o0.z, r0, cb0[2]
dp4 o0.w, r0, cb0[3]
ret
