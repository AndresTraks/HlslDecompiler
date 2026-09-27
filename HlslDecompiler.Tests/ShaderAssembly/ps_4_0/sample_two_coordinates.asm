ps_4_0
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 1
add r0.xy, v0.xy, v0.xy
sample r0, r0.xyxx, t0, s0
mov o0.yz, r0.yz
sample r0, v0.xyxx, t0, s0
mov o0.x, r0.y
mov o0.w, l(1)
ret
