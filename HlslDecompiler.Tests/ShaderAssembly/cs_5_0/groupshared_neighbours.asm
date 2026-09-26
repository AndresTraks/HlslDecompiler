cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 4
dcl_uav_structured u0, 4
dcl_input vThreadIDInGroupFlattened
dcl_input vThreadID.x
dcl_temps 1
dcl_tgsm_structured g0, 4, 64
dcl_thread_group 64, 1, 1
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, vThreadID.x, l(0), t0.x
store_structured g0.x, vThreadIDInGroupFlattened.x, l(0), r0.x
sync_g_t
ld_structured r0.x, vThreadIDInGroupFlattened.x, l(0), g0.x
iadd r0.yz, vThreadIDInGroupFlattened.xx, l(0, 1, 2, 0)
and r0.yz, r0.yz, l(0, 63, 63, 0)
ld_structured r0.y, r0.y, l(0), g0.x
ld_structured r0.z, r0.z, l(0), g0.x
add r0.x, r0.y, r0.x
add r0.x, r0.z, r0.x
mul r0.x, r0.x, l(0.333333343)
store_structured u0.x, vThreadID.x, l(0), r0.x
ret
