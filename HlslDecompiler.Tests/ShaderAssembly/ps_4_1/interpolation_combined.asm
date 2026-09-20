ps_4_1
dcl_globalFlags refactoringAllowed
dcl_input_ps linear noperspective centroid v0.xy
dcl_input_ps linear noperspective sample v1.xy
dcl_input_ps linear centroid v2.x
dcl_output o0
dcl_temps 1
mov r0.xy, v0.xy
mov r0.zw, v1.xy
mul o0, r0, v2.x
ret
