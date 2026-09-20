ps_4_0
dcl_sampler s0, mode_default
dcl_resource_texture2d (unorm,unorm,unorm,unorm) t0
dcl_resource_texture2d (snorm,snorm,snorm,snorm) t1
dcl_resource_texture2d (sint,sint,sint,sint) t2
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 3
ftoi r0.xy, v0.xy
mov r0.zw, l(0, 0, 0, 0)
ld r0, r0, t2
itof r0, r0
sample r1, v0.xyxx, t0, s0
sample r2, v0.xyxx, t1, s0
add r1, r1, r2
add o0, r0, r1
ret
