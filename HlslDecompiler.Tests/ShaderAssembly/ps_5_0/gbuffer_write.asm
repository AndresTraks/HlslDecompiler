ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[2], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_texture2d (float,float,float,float) t1
dcl_resource_texture2d (float,float,float,float) t2
dcl_input_ps linear v1.xyz
dcl_input_ps linear v2
dcl_input_ps linear v3.xy
dcl_input_ps linear v4.xyz
dcl_output o0
dcl_output o1
dcl_output o2.xy
dcl_temps 5
sample_indexable(texture2d)(float,float,float,float) r0, v3.xyxx, t0, s0
mul r1.xyz, r0.xyz, cb0[0].xyz
sample_indexable(texture2d)(float,float,float,float) r2, v3.xyxx, t2, s0
switch cb0[1].z
case l(0)
mad r0.w, r0.w, cb0[0].w, -cb0[1].w
lt r0.w, r0.w, l(0)
discard_nz r0.w
break
case l(1)
mul r1.xyz, r1.xyz, r2.xxx
break
case l(2)
mad r0.xyz, -r0.xyz, cb0[0].xyz, r2.xyz
mad r1.xyz, r2.www, r0.xyz, r1.xyz
break
default
break
endswitch
mov o0.xyz, r1.xyz
dp3 r0.x, v1.xyz, v1.xyz
rsq r0.x, r0.x
mul r0.xyz, r0.xxx, v1.xyz
dp3 r0.w, v2.xyz, r0.xyz
mad r1.xyz, -r0.xyz, r0.www, v2.xyz
dp3 r0.w, r1.xyz, r1.xyz
rsq r0.w, r0.w
mul r1.xyz, r0.www, r1.xyz
mul r3.xyz, r0.zxy, r1.yzx
mad r3.xyz, r0.yzx, r1.zxy, -r3.xyz
mul r3.xyz, r3.xyz, v2.www
sample_indexable(texture2d)(float,float,float,float) r4.xy, v3.xy, t1.xy, s0
mad r4.xy, r4.xy, l(2, 2, 0, 0), l(-1, -1, 0, 0)
dp2 r0.w, r4.xy, r4.xy
add r0.w, -r0.w, l(1)
max r0.w, r0.w, l(0)
sqrt r0.w, r0.w
mul r3.xyz, r3.xyz, r4.yyy
mad r1.xyz, r4.xxx, r1.xyz, r3.xyz
mad r0.xyz, r0.www, r0.xyz, r1.xyz
dp3 r0.w, r0.xyz, r0.xyz
rsq r0.w, r0.w
mul r0.xyz, r0.www, r0.xyz
dp3 r0.w, v4.xyz, v4.xyz
rsq r0.w, r0.w
mul r1.xyz, r0.www, v4.xyz
dp3_sat r0.w, r0.xyz, r1.xyz
add r0.w, -r0.w, l(1)
mul r1.x, r0.w, r0.w
mul r1.x, r1.x, r1.x
mul o0.w, r0.w, r1.x
mad o1.xyz, r0.xyz, l(0.5, 0.5, 0.5, 0), l(0.5, 0.5, 0.5, 0)
ieq r0.x, cb0[1].z, l(2)
movc o1.w, r0.x, r2.w, l(1)
mul o2.xy, r2.yz, cb0[1].xy
ret
