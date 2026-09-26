cs_5_0
dcl_globalFlags refactoringAllowed | enableDoublePrecisionFloatOps
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_structured t0, 8
dcl_uav_structured u0, 8
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 64, 1, 1
ld_structured_indexable(structured_buffer, stride=8)(mixed,mixed,mixed,mixed) r0.xy, vThreadID.xx, l(0), t0.xy
dmul r0.xy, r0.xy, cb0[0].xy
dadd r0.xy, r0.xy, cb0[0].zw
store_structured u0.xy, vThreadID.xx, l(0), r0.xy
ret
