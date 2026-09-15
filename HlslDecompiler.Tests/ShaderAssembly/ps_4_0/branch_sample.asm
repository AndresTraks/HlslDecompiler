ps_4_0
dcl_constantbuffer cb0[1], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_resource_texture2d (float,float,float,float) t1
dcl_input_ps linear v0.xy
dcl_input_ps linear v0.z
dcl_output o0
dcl_temps 3
if_nz cb0[0].x
sample r0, v0.xyxx, t0, s0
mul r1.xy, v0.xy, l(8, 8, 0, 0)
sample r1, r1.xyxx, t1, s0
mul r0, r0, r1
else
sample r0, v0.xyxx, t0, s0
endif
add r1.x, -cb0[0].y, cb0[0].z
add r1.y, v0.z, -cb0[0].y
div r1.x, l(1, 1, 1, 1), r1.x
mul_sat r1.x, r1.x, r1.y
mad r1.y, r1.x, l(-2), l(3)
mul r1.x, r1.x, r1.x
mul r1.x, r1.x, r1.y
add r2, -r0, l(0.5, 0.5, 0.5, 1)
mad o0, r1.x, r2, r0
ret
