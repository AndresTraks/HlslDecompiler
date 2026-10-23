ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v1.xy
dcl_output o0
dcl_temps 4
sample_l_indexable(texture2d)(float,float,float,float) r0.xyz, v1.xyx, t0.xyz, s0, l(0)
dp3 r0.w, r0.xyz, l(0.298999995, 0.587000012, 0.114000000, 0)
sample_l_aoffimmi_indexable(0,-1,0)(texture2d)(float,float,float,float) r1.xyz, v1.xyx, t0.xyz, s0, l(0)
dp3 r1.x, r1.xyz, l(0.298999995, 0.587000012, 0.114000000, 0)
sample_l_aoffimmi_indexable(0,1,0)(texture2d)(float,float,float,float) r1.yzw, v1.yxx, t0.xyz, s0, l(0)
dp3 r1.y, r1.yzw, l(0.298999995, 0.587000012, 0.114000000, 0)
sample_l_aoffimmi_indexable(1,0,0)(texture2d)(float,float,float,float) r2.xyz, v1.xyx, t0.xyz, s0, l(0)
dp3 r1.z, r2.xyz, l(0.298999995, 0.587000012, 0.114000000, 0)
sample_l_aoffimmi_indexable(-1,0,0)(texture2d)(float,float,float,float) r2.xyz, v1.xyx, t0.xyz, s0, l(0)
dp3 r1.w, r2.xyz, l(0.298999995, 0.587000012, 0.114000000, 0)
min r2.xy, r1.yw, r1.xz
min r2.x, r2.y, r2.x
min r2.x, r0.w, r2.x
max r2.yz, r1.yw, r1.xz
max r2.y, r2.z, r2.y
max r2.y, r0.w, r2.y
add r2.x, -r2.x, r2.y
mul r2.y, r2.y, cb0[0].z
lt r2.x, r2.x, r2.y
if_nz r2.x
mov o0.xyz, r0.xyz
mov o0.w, l(1)
ret
endif
add r2.xy, r1.yw, r1.xz
mad r2.x, -r0.w, l(2), r2.x
mad r2.y, -r0.w, l(2), r2.y
ge r2.x, |r2.x|, |r2.y|
movc r3.x, r2.x, l(0), cb0[0].x
and r3.y, r2.x, cb0[0].y
add r1.xyw, -r0.www, r1.xyw
movc r1.x, r2.x, |r1.x|, |r1.w|
add r0.w, -r0.w, r1.z
movc r0.w, r2.x, |r1.y|, |r0.w|
ge r0.w, r1.x, r0.w
movc r0.w, r0.w, l(-0.5), l(0.5)
mad r1.xy, r3.xy, r0.ww, v1.xy
sample_l_indexable(texture2d)(float,float,float,float) r1.xyz, r1.xyx, t0.xyz, s0, l(0)
add r1.xyz, -r0.xyz, r1.xyz
mad o0.xyz, r1.xyz, l(0.5, 0.5, 0.5, 0), r0.xyz
mov o0.w, l(1)
ret
