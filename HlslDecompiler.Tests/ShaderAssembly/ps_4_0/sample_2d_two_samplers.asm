ps_4_0
dcl_sampler s0, mode_default
dcl_sampler s1, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 1
sample r0, v0.yxyy, t0.xyzw, s1
mad r0.xy, r0.xyxx, l(2, 2, 0, 0), v0.yxyy
sample r0, r0.xyxx, t0.xyzw, s0
mov o0, r0.wzyx
ret
