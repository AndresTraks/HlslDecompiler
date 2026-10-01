ps_5_0
dcl_globalFlags refactoringAllowed
dcl_sampler s0, mode_default
dcl_sampler s1, mode_comparison
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 2
gather4_c_indexable(texture2d)(float,float,float,float) r0, v0.xyxx, t0, s1.x, l(0.5)
gather4_aoffimmi_indexable(1,2,0)(texture2d)(float,float,float,float) r1, v0.xyxx, t0, s0.x
add r0, r0, r1
gather4_c_aoffimmi_indexable(3,4,0)(texture2d)(float,float,float,float) r1, v0.xyxx, t0, s1.x, l(0.5)
add o0, r0, r1
ret
