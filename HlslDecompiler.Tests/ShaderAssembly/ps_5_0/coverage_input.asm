ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v0
dcl_input vCoverage
dcl_output o0
dcl_temps 1
countbits r0.x, vCoverage.x
utof r0.x, r0.x
mul r0.x, r0.x, l(0.03125)
mul o0, r0.x, v0
ret
