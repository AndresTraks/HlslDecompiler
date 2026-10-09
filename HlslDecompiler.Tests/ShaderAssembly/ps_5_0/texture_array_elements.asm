ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_texture2d (float,float,float,float) t1
dcl_resource_texture2d (float,float,float,float) t2
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 2
sample_indexable(texture2d)(float,float,float,float) r0, v0.xyxx, t1, s0
mul r0, r0, cb0[0].y
sample_indexable(texture2d)(float,float,float,float) r1, v0.xyxx, t0, s0
mad r0, r1, cb0[0].x, r0
mul r1.xy, v0.xy, l(4, 4, 0, 0)
sample_indexable(texture2d)(float,float,float,float) r1, r1.xyxx, t2, s0
mad o0, r1, cb0[0].z, r0
ret
