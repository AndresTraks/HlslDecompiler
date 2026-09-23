ps_5_0
dcl_globalFlags refactoringAllowed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.y
dcl_input_ps linear v0.z
dcl_input_ps linear v1
dcl_output o0
dcl_temps 2
sample_indexable(texture2d)(float,float,float,float) r0, v0.zyzz, t0, s0
sample_indexable(texture2d)(float,float,float,float) r1, v0.yzyy, t0, s0
mul r0, r0, r1
mul o0, r0, v1
ret
