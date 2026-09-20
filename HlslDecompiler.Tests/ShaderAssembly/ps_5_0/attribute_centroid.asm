ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v0.xy
dcl_input_ps linear v1.xy
dcl_output o0
eval_centroid o0.xy, v0.xy
eval_centroid o0.zw, v1.xy
ret
