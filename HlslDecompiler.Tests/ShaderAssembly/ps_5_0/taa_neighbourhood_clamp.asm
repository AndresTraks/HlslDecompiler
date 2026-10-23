ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_texture2d (float,float,float,float) t1
dcl_resource_texture2d (float,float,float,float) t2
dcl_input_ps_siv linear noperspective v0.xy, position
dcl_input_ps linear v1.xy
dcl_output o0
dcl_temps 9
mov r0.zw, l(0, 0, 0, 0)
ftoi r1.xy, v0.xy
iadd r2, r1.xyxy, l(0, -1, -1, -1)
mov r0.xy, r2.zw
ld_indexable(texture2d)(float,float,float,float) r0.xyz, r0.xyz, t0.xyz
dp3 r3.x, r0.xzy, l(0.25, 0.25, 0.5, 0)
dp2 r3.y, r0.xz, l(0.5, -0.5, 0, 0)
dp3 r3.z, r0.xzy, l(-0.25, -0.25, 0.5, 0)
min r0.xyz, r3.xyz, l(1000000000, 1000000000, 1000000000, 0)
max r3.xyz, r3.xyz, l(-1000000000, -1000000000, -1000000000, 0)
mov r2.zw, l(0, 0, 0, 0)
ld_indexable(texture2d)(float,float,float,float) r2.xyz, r2.xyz, t0.xyz
dp3 r4.x, r2.xzy, l(0.25, 0.25, 0.5, 0)
dp2 r4.y, r2.xz, l(0.5, -0.5, 0, 0)
dp3 r4.z, r2.xzy, l(-0.25, -0.25, 0.5, 0)
min r0.xyz, r0.xyz, r4.xyz
max r2.xyz, r3.xyz, r4.xyz
mov r3.zw, l(0, 0, 0, 0)
iadd r4, r1.xyxy, l(-1, 0, 1, -1)
mov r3.xy, r4.zw
ld_indexable(texture2d)(float,float,float,float) r3.xyz, r3.xyz, t0.xyz
dp3 r5.x, r3.xzy, l(0.25, 0.25, 0.5, 0)
dp2 r5.y, r3.xz, l(0.5, -0.5, 0, 0)
dp3 r5.z, r3.xzy, l(-0.25, -0.25, 0.5, 0)
min r0.xyz, r0.xyz, r5.xyz
max r2.xyz, r2.xyz, r5.xyz
mov r4.zw, l(0, 0, 0, 0)
ld_indexable(texture2d)(float,float,float,float) r3.xyz, r4.xyz, t0.xyz
dp3 r4.x, r3.xzy, l(0.25, 0.25, 0.5, 0)
dp2 r4.y, r3.xz, l(0.5, -0.5, 0, 0)
dp3 r4.z, r3.xzy, l(-0.25, -0.25, 0.5, 0)
min r0.xyz, r0.xyz, r4.xyz
max r2.xyz, r2.xyz, r4.xyz
mov r1.zw, l(0, 0, 0, 0)
ld_indexable(texture2d)(float,float,float,float) r3.xyz, r1.xyw, t0.xyz
ld_indexable(texture2d)(float,float,float,float) r1.zw, r1.zw, t2.xy
add r1.zw, -r1.zw, v1.xy
dp3 r4.x, r3.xzy, l(0.25, 0.25, 0.5, 0)
dp2 r4.y, r3.xz, l(0.5, -0.5, 0, 0)
dp3 r4.z, r3.xzy, l(-0.25, -0.25, 0.5, 0)
min r0.xyz, r0.xyz, r4.xyz
iadd r3, r1.xyxy, l(-1, 1, 1, 0)
iadd r5, r1.xyxy, l(1, 1, 0, 1)
mov r6.xy, r3.zw
mov r6.zw, l(0, 0, 0, 0)
ld_indexable(texture2d)(float,float,float,float) r6.xyz, r6.xyz, t0.xyz
dp3 r7.x, r6.xzy, l(0.25, 0.25, 0.5, 0)
dp2 r7.y, r6.xz, l(0.5, -0.5, 0, 0)
dp3 r7.z, r6.xzy, l(-0.25, -0.25, 0.5, 0)
min r0.xyz, r0.xyz, r7.xyz
mov r3.zw, l(0, 0, 0, 0)
ld_indexable(texture2d)(float,float,float,float) r3.xyz, r3.xyz, t0.xyz
dp3 r6.x, r3.xzy, l(0.25, 0.25, 0.5, 0)
dp2 r6.y, r3.xz, l(0.5, -0.5, 0, 0)
dp3 r6.z, r3.xzy, l(-0.25, -0.25, 0.5, 0)
min r0.xyz, r0.xyz, r6.xyz
mov r3.xy, r5.zw
mov r3.zw, l(0, 0, 0, 0)
ld_indexable(texture2d)(float,float,float,float) r3.xyz, r3.xyz, t0.xyz
dp3 r8.x, r3.xzy, l(0.25, 0.25, 0.5, 0)
dp2 r8.y, r3.xz, l(0.5, -0.5, 0, 0)
dp3 r8.z, r3.xzy, l(-0.25, -0.25, 0.5, 0)
min r0.xyz, r0.xyz, r8.xyz
mov r5.zw, l(0, 0, 0, 0)
ld_indexable(texture2d)(float,float,float,float) r3.xyz, r5.xyz, t0.xyz
dp3 r5.x, r3.xzy, l(0.25, 0.25, 0.5, 0)
dp2 r5.y, r3.xz, l(0.5, -0.5, 0, 0)
dp3 r5.z, r3.xzy, l(-0.25, -0.25, 0.5, 0)
min r0.xyz, r0.xyz, r5.xyz
sample_l_indexable(texture2d)(float,float,float,float) r3.xyz, r1.zwz, t1.xyz, s0, l(0)
add r1.xy, r1.zw, l(-0.5, -0.5, 0, 0)
lt r1.xy, l(0.5, 0.5, 0, 0), |r1.xy|
or r0.w, r1.y, r1.x
movc r0.w, r0.w, l(1), cb0[0].z
dp3 r1.x, r3.xzy, l(0.25, 0.25, 0.5, 0)
dp2 r1.y, r3.xz, l(0.5, -0.5, 0, 0)
dp3 r1.z, r3.xzy, l(-0.25, -0.25, 0.5, 0)
max r0.xyz, r0.xyz, r1.xyz
max r1.xyz, r2.xyz, r4.xyz
max r1.xyz, r7.xyz, r1.xyz
max r1.xyz, r6.xyz, r1.xyz
max r1.xyz, r8.xyz, r1.xyz
max r1.xyz, r5.xyz, r1.xyz
min r0.xyz, r0.xyz, r1.xyz
add r1.xyz, -r0.xyz, r4.xyz
mad r0.xyz, r0.www, r1.xyz, r0.xyz
add r0.w, -r0.y, r0.x
add r0.xy, r0.yz, r0.xx
add o0.xz, -r0.zz, r0.xw
mov o0.y, r0.y
mov o0.w, l(1)
ret
