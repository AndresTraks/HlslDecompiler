ps_4_0
dcl_constantbuffer CB0[1], immediateIndexed
dcl_sampler s0, mode_default
dcl_sampler s1, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_texture3d (float,float,float,float) t1
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 2
sample r0, v0.xyxx, t0, s0
max r0.xyz, r0.xyz, l(0.0000999999975, 0.0000999999975, 0.0000999999975, 0)
log r0.xyz, r0.xyz
mad r0.xyz, r0.xyz, cb0[0].xxx, cb0[0].yyy
exp r0.xyz, r0.xyz
dp3 r0.w, r0.xyz, l(0.212500006, 0.715399981, 0.0720999986, 0)
add r0.xyz, -r0.www, r0.xyz
mad r0.xyz, cb0[0].zzz, r0.xyz, r0.www
mad r1.xyz, r0.xyz, l(0.9375, 0.9375, 0.9375, 0), l(0.03125, 0.03125, 0.03125, 0)
sample_l r1, r1.xyzx, t1, s1, l(0)
add r1.xyz, -r0.xyz, r1.xyz
mad o0.xyz, cb0[0].www, r1.xyz, r0.xyz
mov o0.w, l(1)
ret
