ps_4_1
dcl_globalFlags refactoringAllowed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 2
lod r0.x, v0.xyxx, t0.y, s0
lod r0.y, v0.xyxx, t0.x, s0
add r0.x, -r0.y, r0.x
sample_l r1, v0.xyxx, t0, s0, r0.y
mul o0, r0.x, r1
ret
