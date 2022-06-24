ps_4_0
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_output o0.xyzw
sample o0.xyzw, v0.xyxx, t0.xyzw, s0
ret
