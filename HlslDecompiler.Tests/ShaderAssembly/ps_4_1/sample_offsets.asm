ps_4_1
dcl_globalFlags refactoringAllowed
dcl_sampler s0, mode_default
dcl_sampler s1, mode_comparison
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 2
sample_l_aoffimmi(1,-1,0) r0, v0.xyxx, t0, s0, l(2)
gather4_aoffimmi(-3,4,0) r1, v0.xyxx, t0, s0.x
add r0, r0, r1
ld_aoffimmi(5,-6,0) r1, l(1, 2, 0, 0), t0
add r0, r0, r1
sample_c_lz_aoffimmi(-7,7,0) r1.x, v0.x, t0.x, s1, l(0.5)
add o0, r0, r1.x
ret
