ps_4_0
dcl_constantbuffer cb0[1], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0
dcl_output o0
dcl_temps 4
mul r0.xy, v0.xy, l(256, 256, 0, 0)
ftoi r0.xy, r0.xy
mov r0.zw, l(0, 0, 0, 0)
ld r0, r0, t0
sample_l r1, v0.xyxx, t0, s0, cb0[0].x
sample_b r2, v0.xyxx, t0, s0, cb0[0].y
sample_d r3, v0.xyxx, t0, s0, v0.zwzz, v0.wzww
mad r1, r2, r3, r1
add o0, -r0, r1
ret
