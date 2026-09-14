ps_4_0
dcl_sampler s0, mode_default
dcl_resource_texture1d (float,float,float,float) t0
dcl_resource_texture3d (float,float,float,float) t1
dcl_input_ps linear v0
dcl_output o0
dcl_temps 3
sample r0, v0.x, t0, s0
sample r1, v0.xyzx, t1, s0
sample_l r2, v0.yzwy, t1, s0, l(2)
mad o0, r0, r1, r2
ret
