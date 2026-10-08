ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v1.xyz
dcl_input_ps linear v2
dcl_input_ps linear v3.xy
dcl_output o0
dcl_temps 4
dp3 r0.x, v1.xyz, v1.xyz
rsq r0.x, r0.x
mul r0.xyz, r0.xxx, v1.xyz
dp3 r0.w, v2.xyz, r0.xyz
mad r1.xyz, -r0.yzx, r0.www, v2.yzx
dp3 r0.w, r1.xyz, r1.xyz
rsq r0.w, r0.w
mul r1.xyz, r0.www, r1.xyz
mul r2.xyz, r0.zxy, r1.xyz
mad r2.xyz, r0.yzx, r1.yzx, -r2.xyz
mul r2.xyz, r2.xyz, v2.www
sample_indexable(texture2d)(float,float,float,float) r3.xyz, v3.xyx, t0.xyz, s0
mad r3.xyz, r3.xyz, l(2, 2, 2, 0), l(-1, -1, -1, 0)
mul r2.xyz, r2.xyz, r3.yyy
mad r1.xyz, r1.zxy, r3.xxx, r2.xyz
mad r0.xyz, r0.xyz, r3.zzz, r1.xyz
dp3 r0.w, r0.xyz, r0.xyz
rsq r0.w, r0.w
mul r0.xyz, r0.www, r0.xyz
mad o0.xyz, r0.xyz, l(0.5, 0.5, 0.5, 0), l(0.5, 0.5, 0.5, 0)
dp3_sat o0.w, cb0[0].xyz, r0.xyz
ret
