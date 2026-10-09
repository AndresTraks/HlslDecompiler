cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 4
dcl_uav_structured u0, 4
dcl_input vThreadIDInGroupFlattened
dcl_input vThreadGroupID.x
dcl_input vThreadID.x
dcl_temps 2
dcl_tgsm_structured g0, 4, 128
dcl_thread_group 128, 1, 1
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, vThreadID.x, l(0), t0.x
store_structured g0.x, vThreadIDInGroupFlattened.x, l(0), r0.x
sync_g_t
ult r1, vThreadIDInGroupFlattened.x, l(64, 32, 16, 8)
if_nz r1.x
iadd r0.y, vThreadIDInGroupFlattened.x, l(64)
ld_structured r0.y, r0.y, l(0), g0.x
add r0.x, r0.y, r0.x
store_structured g0.x, vThreadIDInGroupFlattened.x, l(0), r0.x
endif
sync_g_t
if_nz r1.y
iadd r0.x, vThreadIDInGroupFlattened.x, l(32)
ld_structured r0.x, r0.x, l(0), g0.x
ld_structured r0.y, vThreadIDInGroupFlattened.x, l(0), g0.x
add r0.x, r0.x, r0.y
store_structured g0.x, vThreadIDInGroupFlattened.x, l(0), r0.x
endif
sync_g_t
if_nz r1.z
iadd r0.x, vThreadIDInGroupFlattened.x, l(16)
ld_structured r0.x, r0.x, l(0), g0.x
ld_structured r0.y, vThreadIDInGroupFlattened.x, l(0), g0.x
add r0.x, r0.x, r0.y
store_structured g0.x, vThreadIDInGroupFlattened.x, l(0), r0.x
endif
sync_g_t
if_nz r1.w
iadd r0.x, vThreadIDInGroupFlattened.x, l(8)
ld_structured r0.x, r0.x, l(0), g0.x
ld_structured r0.y, vThreadIDInGroupFlattened.x, l(0), g0.x
add r0.x, r0.x, r0.y
store_structured g0.x, vThreadIDInGroupFlattened.x, l(0), r0.x
endif
sync_g_t
ult r0.xyz, vThreadIDInGroupFlattened.xxx, l(4, 2, 1, 0)
if_nz r0.x
iadd r0.x, vThreadIDInGroupFlattened.x, l(4)
ld_structured r0.x, r0.x, l(0), g0.x
ld_structured r0.w, vThreadIDInGroupFlattened.x, l(0), g0.x
add r0.x, r0.x, r0.w
store_structured g0.x, vThreadIDInGroupFlattened.x, l(0), r0.x
endif
sync_g_t
if_nz r0.y
iadd r0.x, vThreadIDInGroupFlattened.x, l(2)
ld_structured r0.x, r0.x, l(0), g0.x
ld_structured r0.y, vThreadIDInGroupFlattened.x, l(0), g0.x
add r0.x, r0.x, r0.y
store_structured g0.x, vThreadIDInGroupFlattened.x, l(0), r0.x
endif
sync_g_t
if_nz r0.z
ld_structured r0.x, l(1), l(0), g0.x
ld_structured r0.y, l(0), l(0), g0.x
add r0.x, r0.x, r0.y
store_structured g0.x, l(0), l(0), r0.x
endif
sync_g_t
if_z vThreadIDInGroupFlattened.x
ld_structured r0.x, l(0), l(0), g0.x
store_structured u0.x, vThreadGroupID.x, l(0), r0.x
endif
ret
