vs_5_0
dcl_globalFlags refactoringAllowed | enableDoublePrecisionFloatOps
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input v0
dcl_output_siv o0, position
dcl_output o1.xy
dcl_temps 1
mov o0, v0
ftod r0.xy, v0.x
dmul r0.xy, r0.xy, cb0[0].xy
dadd r0.xy, r0.xy, d(1.000000l, 0.000000l)
mov o1.xy, r0.xy
ret
