ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[2], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_texture2d (float,float,float,float) t1
dcl_resource_texturecube (float,float,float,float) t2
dcl_input_ps linear v1.xyz
dcl_input_ps linear v2.xyz
dcl_input_ps linear v3.xy
dcl_output o0
dcl_temps 6
add r0.xyz, -v1.xyz, cb0[1].xyz
dp3 r0.w, r0.xyz, r0.xyz
rsq r0.w, r0.w
mad r1.xyz, r0.xyz, r0.www, -cb0[0].xyz
mul r0.xyz, r0.www, r0.xyz
dp3 r0.w, r1.xyz, r1.xyz
rsq r0.w, r0.w
mul r1.xyz, r0.www, r1.xyz
dp3 r0.w, v2.xyz, v2.xyz
rsq r0.w, r0.w
mul r2.xyz, r0.www, v2.xyz
dp3_sat r0.w, r2.xyz, r1.xyz
dp3_sat r1.x, r0.xyz, r1.xyz
add r1.x, -r1.x, l(1)
mul r0.w, r0.w, r0.w
sample_indexable(texture2d)(float,float,float,float) r1.y, v3.y, t1.x, s0
mul r1.z, r1.y, r1.y
max r1.z, r1.z, l(0.00200000009)
mad r1.w, r1.z, r1.z, l(-1)
mad r0.w, r0.w, r1.w, l(1)
mul r0.w, r0.w, r0.w
mul r0.w, r0.w, l(3.14159274)
mul r1.w, r1.z, r1.z
div r0.w, r1.w, r0.w
mul r1.yw, r1.yz, l(0, 8, 0, 0.5)
mad r1.z, -r1.z, l(0.5), l(1)
dp3_sat r2.w, r2.xyz, r0.xyz
mad r2.w, r2.w, r1.z, r1.w
dp3_sat r3.x, r2.xyz, -cb0[0].xyz
mad r1.z, r3.x, r1.z, r1.w
mul r1.z, r2.w, r1.z
div r1.z, l(1, 1, 1, 1), r1.z
mul r0.w, r0.w, r1.z
mul r1.z, r1.x, r1.x
mul r1.z, r1.z, r1.z
mul r1.x, r1.z, r1.x
sample_indexable(texture2d)(float,float,float,float) r3.yzw, v3.yxx, t0.xyz, s0
add r4.xyz, r3.yzw, l(-0.0399999991, -0.0399999991, -0.0399999991, 0)
mad r4.xyz, cb0[1].www, r4.xyz, l(0.0399999991, 0.0399999991, 0.0399999991, 0)
add r5.xyz, -r4.xyz, l(1, 1, 1, 0)
mad r1.xzw, r5.xyz, r1.xxx, r4.xyz
mul r5.xyz, r0.www, r1.xzw
add r1.xzw, -r1.xzw, l(1, 0, 1, 1)
mul r5.xyz, r5.xyz, l(0.25, 0.25, 0.25, 0)
add r0.w, -cb0[1].w, l(1)
mul r3.yzw, r0.www, r3.yzw
mad r1.xzw, r3.yzw, r1.xzw, r5.xyz
mul r1.xzw, r3.xxx, r1.xzw
dp3 r0.w, -r0.xyz, r2.xyz
add r0.w, r0.w, r0.w
mad r0.xyz, r2.xyz, -r0.www, -r0.xyz
sample_l_indexable(texturecube)(float,float,float,float) r0.xyz, r0.xyz, t2.xyz, s0, r1.yyy
mul r0.xyz, r4.xyz, r0.xyz
mad o0.xyz, r1.xzw, cb0[0].www, r0.xyz
mov o0.w, l(1)
ret
