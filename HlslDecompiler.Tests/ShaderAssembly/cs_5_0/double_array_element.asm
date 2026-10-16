cs_5_0
dcl_globalFlags refactoringAllowed | enableDoublePrecisionFloatOps
dcl_resource_structured t0, 32
dcl_uav_structured u0, 32
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 16, 1, 1
ld_structured_indexable(structured_buffer, stride=32)(mixed,mixed,mixed,mixed) r0.xy, vThreadID.xx, l(24), t0.xy
dmul r0.xy, r0.xy, d(2.000000l, 0.000000l)
ld_structured_indexable(structured_buffer, stride=32)(mixed,mixed,mixed,mixed) r0.z, vThreadID.x, l(0), t0.x
ftod r0.zw, r0.z
dadd r0.xy, r0.zw, r0.xy
store_structured u0.xy, vThreadID.xx, l(16), r0.xy
ret
