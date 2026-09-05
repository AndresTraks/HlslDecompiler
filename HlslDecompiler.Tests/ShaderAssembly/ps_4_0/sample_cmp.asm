ps_4_0
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.xyz
dcl_output o0
dcl_temps 1
SampleCLZ
mov o0, r0.x
ret
