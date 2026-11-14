ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[2], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 1
mad r0, v0, cb0[0], cb0[1]
mul o0, r0.w, r0
ret
