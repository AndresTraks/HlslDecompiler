ps_4_0
dcl_constantbuffer CB0[1], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v1.xy
dcl_output o0
dcl_output oDepth
dcl_temps 2
sample r0, v1.xyxx, t0, s0
lt r1.x, r0.w, cb0[0].x
discard_nz r1.x
lt r1.x, l(0.5), r0.x
add r1.y, r0.x, r0.x
add r1.z, -cb0[0].y, cb0[0].z
mad r1.y, r1.y, r1.z, cb0[0].y
movc oDepth, r1.x, cb0[0].y, r1.y
mov o0, r0
ret
