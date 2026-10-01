ps_5_0
dcl_globalFlags refactoringAllowed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_output o0
dcl_output o1
dcl_temps 1
dp2 r0.x, v0.xy, v0.xy
sqrt o0.xy, r0.xx
mul o0.zw, v0.xx, l(0, 0, 3, 3)
lod r0.x, v0.xyxx, t0.x, s0
sampleinfo r0.yz, rasterizer.xx
mov o1, r0.xxyz
ret
