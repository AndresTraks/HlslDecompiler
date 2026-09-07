ps_4_0
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 1
deriv_rtx r0.xy, v0.xy
deriv_rty r0.zw, v0.xy
sample_d o0, v0.xyxx, t0, s0, r0.xyxx, r0.zwzz
ret
