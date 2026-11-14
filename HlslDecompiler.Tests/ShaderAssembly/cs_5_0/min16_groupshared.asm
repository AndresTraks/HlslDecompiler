cs_5_0
dcl_globalFlags refactoringAllowed | enableMinimumPrecision
dcl_resource_structured t0, 16
dcl_uav_structured u0, 16
dcl_input vThreadIDInGroupFlattened
dcl_input vThreadID.x
dcl_temps 2
dcl_tgsm_structured g0, 16, 64
dcl_thread_group 64, 1, 1
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r0, vThreadID.x, l(0), t0
store_structured g0, vThreadIDInGroupFlattened.x, l(0), r0
sync_g_t
iadd r1.x, vThreadIDInGroupFlattened.x, l(1)
and r1.x, r1.x, l(63)
ld_structured r1, r1.x, l(0), g0
add r0, r0, r1
store_structured u0, vThreadID.x, l(0), r0
ret
