cs_5_0
dcl_globalFlags refactoringAllowed | enableDoublePrecisionFloatOps
dcl_resource_structured t0, 32
dcl_uav_structured u0, 8
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 64, 1, 1
ld_structured_indexable(structured_buffer, stride=32)(mixed,mixed,mixed,mixed) r0.xy, vThreadID.xx, l(24), t0.xy
store_structured u0.xy, vThreadID.xx, l(0), r0.xy
ld_structured_indexable(structured_buffer, stride=32)(mixed,mixed,mixed,mixed) r0.xy, vThreadID.xx, l(0), t0.xy
iadd r0.z, vThreadID.x, l(1)
store_structured u0.xy, r0.zz, l(0), r0.xy
ret
