ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[2], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 2
sample_indexable(texture2d)(float,float,float,float) r0, v0.xyxx, t0, s0
ieq r1.x, cb0[0].x, l(2)
if_nz r1.x
dp3 r1.x, r0.xyz, l(0.298999995, 0.587000012, 0.114000000, 0)
mov r1.y, r0.w
mov o0, r1.xxxy
ret
endif
mul o0, r0, cb0[1]
ret
