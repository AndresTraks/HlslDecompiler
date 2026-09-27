cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 16
dcl_uav_structured u0, 16
dcl_input vThreadIDInGroupFlattened
dcl_input vThreadID.x
dcl_temps 2
dcl_tgsm_structured g0, 20, 32
dcl_thread_group 32, 1, 1
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r0, vThreadID.x, l(0), t0
store_structured g0, vThreadIDInGroupFlattened.x, l(0), r0
iadd r0.x, vThreadID.x, l(7)
utof r0.x, r0.x
store_structured g0.x, vThreadIDInGroupFlattened.x, l(16), r0.x
sync_uglobal_g_t
iadd r0.x, vThreadIDInGroupFlattened.x, l(1)
and r0.x, r0.x, l(31)
ld_structured r1, r0.x, l(0), g0
ld_structured r0.x, r0.x, l(16), g0.x
div r0, r1, r0.x
store_structured u0, vThreadID.x, l(0), r0
ret
