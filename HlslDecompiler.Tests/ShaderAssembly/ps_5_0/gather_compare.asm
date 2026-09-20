ps_5_0
dcl_globalFlags refactoringAllowed
dcl_sampler s0, mode_comparison
dcl_sampler s1, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_texture2d (float,float,float,float) t1
dcl_input_ps linear v0.xyz
dcl_output o0
dcl_temps 2
gather4_c_indexable(texture2d)(float,float,float,float) r0, v0.xyxx, t0, s0.x, v0.z
gather4_aoffimmi_indexable(1,-1,0)(texture2d)(float,float,float,float) r1, v0.xyxx, t1, s1.x
add o0, r0, r1
ret
