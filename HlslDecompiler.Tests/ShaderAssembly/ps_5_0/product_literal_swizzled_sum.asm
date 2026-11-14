ps_5_0
dcl_globalFlags refactoringAllowed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 1
sample_indexable(texture2d)(float,float,float,float) r0, v0.xyxx, t0, s0
mul r0, r0, l(0.25, 0.5, 0.125, 2)
add o0, r0.yzwx, r0
ret
