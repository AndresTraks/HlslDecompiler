cs_5_0
dcl_globalFlags refactoringAllowed | enableDoublePrecisionFloatOps
dcl_constantbuffer CB0[1], immediateIndexed
dcl_uav_structured u0, 4
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 32, 1, 1
mov r0.xy, cb0[0].xy
dmul r0.xy, r0.xy, cb0[0].zw
dtof r0.x, r0.xy
store_structured u0.x, vThreadID.x, l(0), r0.x
ret
