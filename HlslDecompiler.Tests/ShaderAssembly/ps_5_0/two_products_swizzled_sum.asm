ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[2], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 2
sample_indexable(texture2d)(float,float,float,float) r0, v0.xyxx, t0, s0
mul r1, r0.yzwx, cb0[0].yzwx
mad r1, r0.yzwx, cb0[1].yzwx, r1.wxyz
mad o0, r0.yzwx, cb0[0].yzwx, r1
ret
