vs_4_0
dcl_constantbuffer CB0[3], immediateIndexed
dcl_input v0.xyz
dcl_output_siv o0, position
dcl_temps 2
imul null, r0.xy, v0.xy, cb0[0].xy
iadd r0.x, r0.y, r0.x
imad r0.x, v0.z, cb0[0].z, r0.x
imul null, r1.xy, v0.xy, cb0[1].xy
iadd r0.w, r1.y, r1.x
imad r0.y, v0.z, cb0[1].z, r0.w
imul null, r1.xy, v0.xy, cb0[2].xy
iadd r0.w, r1.y, r1.x
imad r0.z, v0.z, cb0[2].z, r0.w
ineg r1.xyz, r0.xyz
movc r0.xyz, cb0[2].www, r1.xyz, r0.xyz
itof o0.xyz, r0.xyz
mov o0.w, l(1)
ret
