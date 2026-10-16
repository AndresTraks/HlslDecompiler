vs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[2], immediateIndexed
dcl_input v0
dcl_output_siv o0, position
dcl_output o1
dcl_temps 1
mul [precise(x)] r0, v0, cb0[0]
add [precise(x)] o0, r0, cb0[1]
mad o1, v0, cb0[0], cb0[1]
ret
