cs_5_0
dcl_globalFlags refactoringAllowed | enableDoublePrecisionFloatOps
dcl_resource_structured t0, 8
dcl_uav_structured u0, 8
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 32, 1, 1
ld_structured_indexable(structured_buffer, stride=8)(mixed,mixed,mixed,mixed) r0.xy, vThreadID.xx, l(0), t0.xy
dmul r0.xy, r0.xy, d(2.000000l, 0.000000l)
store_structured u0.xy, vThreadID.xx, l(0), r0.xy
ret
