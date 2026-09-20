ps_4_0
dcl_sampler s0, mode_default
dcl_resource_texture1darray (float,float,float,float) t0
dcl_input_ps linear v0
dcl_output o0
dcl_temps 2
sample r0, v0.xyxx, t0, s0
sample_l r1, v0.zwzz, t0, s0, l(2)
mul o0, r0, r1
ret
