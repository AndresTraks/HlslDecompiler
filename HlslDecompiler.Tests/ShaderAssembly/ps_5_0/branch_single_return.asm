ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 2
sample_l_indexable(texture2d)(float,float,float,float) r0, v0.xyxx, t0, s0, l(0)
lt r1.x, cb0[0].x, v0.x
if_nz r1.x
add r1.xy, v0.xy, v0.xy
sample_l_indexable(texture2d)(float,float,float,float) r1, r1.xyxx, t0, s0, l(1)
mul o0, r0, r1
else
mov o0, r0
endif
ret
