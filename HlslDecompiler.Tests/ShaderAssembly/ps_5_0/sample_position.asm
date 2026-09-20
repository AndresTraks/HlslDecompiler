ps_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_texture2dms(4) (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_input_ps_sgv constant v1.x, sampleIndex
dcl_output o0
samplepos o0.xy, t0.xy, v1.xx
mov o0.zw, v0.xy
ret
