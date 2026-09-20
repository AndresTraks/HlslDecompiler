ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[4], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_texture2d (float,float,float,float) t1
dcl_input_ps linear v1.xyz
dcl_input_ps linear v2.xyz
dcl_input_ps linear v3.xyz
dcl_input_ps linear v4.xy
dcl_output o0
dcl_temps 4
dp3 r0.x, v2.xyz, v2.xyz
rsq r0.x, r0.x
mul r0.xyz, r0.xxx, v2.xyz
dp3 r0.w, v3.xyz, r0.xyz
mad r1.xyz, -r0.xyz, r0.www, v3.xyz
dp3 r0.w, r1.xyz, r1.xyz
rsq r0.w, r0.w
mul r1.xyz, r0.www, r1.xyz
mul r2.xyz, r0.zxy, r1.yzx
mad r2.xyz, r0.yzx, r1.zxy, -r2.xyz
sample_indexable(texture2d)(float,float,float,float) r3.xyz, v4.xyx, t1.xyz, s0
mad r3.xyz, r3.xyz, l(2, 2, 2, 0), l(-1, -1, -1, 0)
mul r2.xyz, r2.xyz, r3.yyy
mad r1.xyz, r3.xxx, r1.xyz, r2.xyz
mad r0.xyz, r3.zzz, r0.xyz, r1.xyz
dp3 r0.w, r0.xyz, r0.xyz
rsq r0.w, r0.w
mul r0.xyz, r0.www, r0.xyz
dp3_sat r0.w, r0.xyz, -cb0[0].xyz
mul r0.w, r0.w, cb0[0].w
add r1.xyz, -v1.xyz, cb0[1].xyz
dp3 r1.w, r1.xyz, r1.xyz
rsq r2.x, r1.w
sqrt r1.w, r1.w
add r1.w, r1.w, -cb0[3].x
mad r1.xyz, r1.xyz, r2.xxx, -cb0[0].xyz
dp3 r2.x, r1.xyz, r1.xyz
rsq r2.x, r2.x
mul r1.xyz, r1.xyz, r2.xxx
dp3_sat r0.x, r0.xyz, r1.xyz
log r0.x, r0.x
mul r0.x, r0.x, l(32)
exp r0.x, r0.x
and r0.yz, cb0[1].ww, l(0, 1, 2, 0)
movc r0.y, r0.y, l(1), l(0)
mul r0.x, r0.y, r0.x
sample_indexable(texture2d)(float,float,float,float) r2, v4.xyxx, t0, s0
mad r0.xyw, r2.xyz, r0.www, r0.xxx
mov o0.w, r2.w
add r1.xyz, -r0.xyw, cb0[2].xyz
add r2.x, -cb0[3].x, cb0[3].y
div_sat r1.w, r1.w, r2.x
mad r1.xyz, r1.www, r1.xyz, r0.xyw
movc o0.xyz, r0.zzz, r1.xyz, r0.xyw
ret
