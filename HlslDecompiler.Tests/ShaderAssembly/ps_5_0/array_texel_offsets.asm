ps_5_0
dcl_globalFlags refactoringAllowed
dcl_sampler s0, mode_comparison
dcl_sampler s1, mode_default
dcl_resource_texture2darray (float,float,float,float) t0
dcl_resource_texture1darray (float,float,float,float) t1
dcl_resource_texture2darray (float,float,float,float) t2
dcl_input_ps linear v0
dcl_input_ps linear v1
dcl_output o0
dcl_temps 2
sample_c_lz_aoffimmi_indexable(1,-1,0)(texture2darray)(float,float,float,float) r0.x, v0.x, t0.x, s0, v0.w
sample_aoffimmi_indexable(3,0,0)(texture1darray)(float,float,float,float) r1, v0.xyxx, t1, s1
add r0, r0.x, r1
sample_l_aoffimmi_indexable(-2,4,0)(texture2darray)(float,float,float,float) r1, v1.xyzx, t2, s1, v1.w
add r0, r0, r1
ftoi r1, v1
ld_aoffimmi_indexable(1,1,0)(texture2darray)(float,float,float,float) r1, r1, t2
add o0, r0, r1
ret
