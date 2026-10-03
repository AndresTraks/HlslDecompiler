ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[13], dynamicIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_texture2d (float,float,float,float) t1
dcl_resource_texture2d (float,float,float,float) t2
dcl_input_ps linear v1.xy
dcl_output o0
dcl_temps 6
sample_indexable(texture2d)(float,float,float,float) r0.xyz, v1.xyx, t1.xyz, s0
mad r0.xyz, r0.xyz, l(2, 2, 2, 0), l(-1, -1, -1, 0)
mul r1.xy, v1.xy, cb0[12].xy
sample_indexable(texture2d)(float,float,float,float) r1.xyz, r1.xyx, t2.xyz, s0
mad r1.xyz, r1.xyz, l(2, 2, 2, 0), l(-1, -1, -1, 0)
sample_indexable(texture2d)(float,float,float,float) r2.z, v1.x, t0.x, s0
dp3 r0.w, r1.xyz, r0.xyz
mad r1.xyz, -r0.xyz, r0.www, r1.xyz
dp3 r0.w, r1.xyz, r1.xyz
rsq r0.w, r0.w
mul r1.xyz, r0.www, r1.xyz
mul r3.xyz, r0.zxy, r1.yzx
mad r3.xyz, r0.yzx, r1.zxy, -r3.xyz
mad r2.xy, v1.xy, l(2, 2, 0, 0), l(-1, -1, 0, 0)
mov r4.w, l(1)
mov r0.w, l(0)
mov r1.w, l(0)
loop
ige r2.w, r1.w, l(8)
breakc_nz r2.w
mul r5.xyz, r3.xyz, cb0[r1.w + 4].yyy
mad r5.xyz, r1.xyz, cb0[r1.w + 4].xxx, r5.xyz
mad r5.xyz, r0.xyz, cb0[r1.w + 4].zzz, r5.xyz
mad r4.xyz, r5.xyz, cb0[12].zzz, r2.xyz
dp4 r5.x, r4, cb0[0]
dp4 r5.y, r4, cb0[1]
dp4 r2.w, r4, cb0[3]
div r4.xy, r5.xy, r2.ww
mad r4.xy, r4.xy, l(0.5, 0.5, 0, 0), l(0.5, 0.5, 0, 0)
sample_indexable(texture2d)(float,float,float,float) r2.w, r4.x, t0.x, s0
add r3.w, r4.z, cb0[12].w
ge r2.w, r2.w, r3.w
and r2.w, r2.w, l(1065353216)
add r0.w, r0.w, r2.w
iadd r1.w, r1.w, l(1)
endloop
mad o0, -r0.w, l(0.125, 0.125, 0.125, 0.125), l(1, 1, 1, 1)
ret
