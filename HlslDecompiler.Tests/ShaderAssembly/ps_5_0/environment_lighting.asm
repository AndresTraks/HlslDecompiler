ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[14], dynamicIndexed
dcl_sampler s0, mode_default
dcl_sampler s1, mode_comparison
dcl_resource_texturecube (float,float,float,float) t0
dcl_resource_texture2darray (float,float,float,float) t1
dcl_resource_texture2d (float,float,float,float) t2
dcl_input_ps_siv linear noperspective v0.w, position
dcl_input_ps linear v1.xyz
dcl_input_ps linear v1.w
dcl_input_ps linear v2.xyz
dcl_input_ps linear v3.xyz
dcl_output o0
dcl_temps 5
dp3 r0.x, v3.xyz, v3.xyz
rsq r0.x, r0.x
mul r0.xyz, r0.xxx, v3.xyz
dp3 r0.w, v2.xyz, v2.xyz
rsq r0.w, r0.w
mul r1.xyz, r0.www, v2.xyz
dp3 r0.w, -r0.xyz, r1.xyz
add r0.w, r0.w, r0.w
mad r2.xyz, r1.xyz, -r0.www, -r0.xyz
dp3_sat r0.x, r1.xyz, r0.xyz
sample_l_indexable(texturecube)(float,float,float,float) r1.xyz, r1.xyz, t0.xyz, s0, l(6)
mul r0.z, v1.w, l(6)
sample_l_indexable(texturecube)(float,float,float,float) r2.xyz, r2.xyz, t0.xyz, s0, r0.zzz
mov r0.y, v1.w
sample_indexable(texture2d)(float,float,float,float) r0.xy, r0.xy, t2.xy, s0
add r0.x, r0.y, r0.x
mad r0.xyz, r2.xyz, r0.xxx, r1.xyz
mul r0.xyz, r0.xyz, cb0[13].xyz
lt r1.xy, v0.ww, cb0[12].xy
movc r0.w, r1.y, l(1), l(2)
movc r0.w, r1.x, l(0), r0.w
ishl r1.x, r0.w, l(2)
utof r2.z, r0.w
mov r3.xyz, v1.xyz
mov r3.w, l(1)
dp4 r4.x, r3, cb0[r1.x]
dp4 r4.y, r3, cb0[r1.x + 1]
dp4 r4.z, r3, cb0[r1.x + 2]
dp4 r0.w, r3, cb0[r1.x + 3]
div r1.xyz, r4.xyz, r0.www
mad r2.xy, r1.xy, l(0.5, -0.5, 0, 0), l(0.5, 0.5, 0, 0)
add r0.w, r1.z, -cb0[12].w
sample_c_lz_indexable(texture2darray)(float,float,float,float) r0.w, r2.x, t1.x, s1, r0.w
mul r0.xyz, r0.www, r0.xyz
mul r0.xyz, -r0.xyz, cb0[13].www
mul r0.xyz, r0.xyz, l(1.44269502, 1.44269502, 1.44269502, 0)
exp r0.xyz, r0.xyz
add r0.xyz, -r0.xyz, l(1, 1, 1, 0)
log r0.xyz, r0.xyz
mul r0.xyz, r0.xyz, l(0.454545468, 0.454545468, 0.454545468, 0)
exp o0.xyz, r0.xyz
mov o0.w, l(1)
ret
