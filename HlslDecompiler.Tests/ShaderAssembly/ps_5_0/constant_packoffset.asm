ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[3], immediateIndexed
dcl_output o0
add o0.xy, cb0[0].zw, cb0[2].xy
mov o0.zw, cb0[2].zw
ret
