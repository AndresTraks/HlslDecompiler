ps_5_0
dcl_globalFlags refactoringAllowed
dcl_immediateConstantBuffer { { 1, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } }
dcl_constantbuffer CB0[22], dynamicIndexed
dcl_sampler s0, mode_comparison
dcl_resource_texture2darray (float,float,float,float) t0
dcl_input_ps linear v1.xyz
dcl_input_ps linear v1.w
dcl_input_ps linear v2.xyz
dcl_output o0
dcl_temps 5
mad r0.xyz, v2.xyz, cb0[17].yyy, v1.xyz
lt r1.xyz, v1.www, cb0[16].zyx
movc r1.x, r1.x, l(2), l(3)
movc r1.x, r1.y, l(1), r1.x
movc r1.x, r1.z, l(0), r1.x
ishl r1.y, r1.x, l(2)
mov r0.w, l(1)
dp4 r2.x, r0, cb0[r1.y]
dp4 r2.y, r0, cb0[r1.y + 1]
dp4 r1.z, r0, cb0[r1.y + 2]
mad r3.xy, cb0[18].xy, cb0[21].zz, r2.xy
itof r3.z, r1.x
add r1.z, r1.z, -cb0[17].x
sample_c_lz_indexable(texture2darray)(float,float,float,float) r1.w, r3.x, t0.x, s0, r1.z
mad r3.xy, cb0[19].xy, cb0[21].zz, r2.xy
sample_c_lz_indexable(texture2darray)(float,float,float,float) r2.z, r3.z, t0.x, s0, r1.z
add r1.w, r1.w, r2.z
mad r3.xy, cb0[20].xy, cb0[21].zz, r2.xy
sample_c_lz_indexable(texture2darray)(float,float,float,float) r2.z, r3.z, t0.x, s0, r1.z
add r1.w, r1.w, r2.z
mad r3.xy, cb0[21].xy, cb0[21].zz, r2.xy
sample_c_lz_indexable(texture2darray)(float,float,float,float) r1.z, r3.z, t0.x, s0, r1.z
add r1.z, r1.z, r1.w
mul r1.w, r1.z, l(0.25)
dp4 r2.x, cb0[16], icb[r1.x]
add r2.x, r2.x, -v1.w
div r2.x, r2.x, cb0[17].z
lt r2.y, r2.x, l(1)
ilt r2.z, r1.x, l(3)
and r2.y, r2.z, r2.y
if_nz r2.y
iadd r1.x, r1.x, l(1)
dp4 r3.x, r0, cb0[r1.y + 4]
dp4 r3.y, r0, cb0[r1.y + 5]
dp4 r0.x, r0, cb0[r1.y + 6]
mad r4.xy, cb0[18].xy, cb0[21].zz, r3.xy
itof r4.z, r1.x
add r0.x, r0.x, -cb0[17].x
sample_c_lz_indexable(texture2darray)(float,float,float,float) r0.y, r4.y, t0.x, s0, r0.x
mad r4.xy, cb0[19].xy, cb0[21].zz, r3.xy
sample_c_lz_indexable(texture2darray)(float,float,float,float) r0.z, r4.z, t0.x, s0, r0.x
add r0.y, r0.z, r0.y
mad r4.xy, cb0[20].xy, cb0[21].zz, r3.xy
sample_c_lz_indexable(texture2darray)(float,float,float,float) r0.z, r4.z, t0.x, s0, r0.x
add r0.y, r0.z, r0.y
mad r4.xy, cb0[21].xy, cb0[21].zz, r3.xy
sample_c_lz_indexable(texture2darray)(float,float,float,float) r0.x, r4.x, t0.x, s0, r0.x
add r0.x, r0.x, r0.y
mul r0.x, r0.x, l(0.25)
mad r0.y, r1.z, l(0.25), -r0.x
mad r1.w, r2.x, r0.y, r0.x
endif
mov o0.xyz, r1.www
mov o0.w, l(1)
ret
