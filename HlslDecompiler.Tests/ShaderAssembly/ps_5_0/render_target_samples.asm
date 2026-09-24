ps_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_texture2dms(4) (float,float,float,float) t0
dcl_output o0
dcl_temps 1
samplepos r0.xy, t0.xy, l(2)
mov o0.w, r0.x
samplepos o0.xy, rasterizer.xy, l(1)
sampleinfo o0.z, rasterizer.x
ret
