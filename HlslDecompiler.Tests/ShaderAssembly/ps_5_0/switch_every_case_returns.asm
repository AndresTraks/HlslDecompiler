ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[2], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 1
sample_indexable(texture2d)(float,float,float,float) r0, v0.xyxx, t0.xwyz, s0
switch cb0[0].x
case l(2)
dp3 r0.x, r0.xzw, l(0.298999995, 0.587000012, 0.114000000, 0)
mov o0, r0.xxxy
ret
default
mul o0, r0.xzwy, cb0[1]
ret
endswitch
ret
