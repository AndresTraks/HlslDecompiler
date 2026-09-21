cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_raw t0
dcl_uav_raw u0
dcl_input vThreadIDInGroupFlattened
dcl_input vThreadID.x
dcl_temps 3
dcl_tgsm_structured g0, 4, 128
dcl_thread_group 128, 1, 1
imul null, r0.x, vThreadID.x, cb0[0].y
ld_raw_indexable(raw_buffer)(mixed,mixed,mixed,mixed) r0.yz, r0.xx, t0.xy
and r0.w, r0.z, l(1)
movc r0.w, r0.w, l(1), l(0)
store_structured g0.x, vThreadIDInGroupFlattened.x, l(0), r0.w
sync_g_t
uge r1, vThreadIDInGroupFlattened.x, l(1, 2, 4, 8)
iadd r2, vThreadIDInGroupFlattened.x, l(-1, -2, -4, -8)
ld_structured r2.x, r2.x, l(0), g0.x
and r1.x, r1.x, r2.x
sync_g_t
iadd r0.w, r0.w, r1.x
store_structured g0.x, vThreadIDInGroupFlattened.x, l(0), r0.w
sync_g_t
ld_structured r1.x, r2.y, l(0), g0.x
and r1.x, r1.x, r1.y
sync_g_t
iadd r0.w, r0.w, r1.x
store_structured g0.x, vThreadIDInGroupFlattened.x, l(0), r0.w
sync_g_t
ld_structured r1.x, r2.z, l(0), g0.x
and r1.x, r1.x, r1.z
sync_g_t
iadd r0.w, r0.w, r1.x
store_structured g0.x, vThreadIDInGroupFlattened.x, l(0), r0.w
sync_g_t
ld_structured r1.x, r2.w, l(0), g0.x
and r1.x, r1.x, r1.w
sync_g_t
iadd r0.w, r0.w, r1.x
store_structured g0.x, vThreadIDInGroupFlattened.x, l(0), r0.w
sync_g_t
uge r1.xy, vThreadIDInGroupFlattened.xx, l(16, 32, 0, 0)
iadd r1.zw, vThreadIDInGroupFlattened.xx, l(0, 0, -16, -32)
ld_structured r1.z, r1.z, l(0), g0.x
and r1.x, r1.z, r1.x
sync_g_t
iadd r0.w, r0.w, r1.x
store_structured g0.x, vThreadIDInGroupFlattened.x, l(0), r0.w
sync_g_t
ld_structured r1.x, r1.w, l(0), g0.x
and r1.x, r1.x, r1.y
sync_g_t
iadd r0.w, r0.w, r1.x
store_structured g0.x, vThreadIDInGroupFlattened.x, l(0), r0.w
sync_g_t
ult r1.x, vThreadID.x, cb0[0].x
if_nz r1.x
mul r1.x, r0.y, cb0[0].z
uge r0.y, vThreadIDInGroupFlattened.x, l(64)
iadd r1.z, vThreadIDInGroupFlattened.x, l(-64)
ld_structured r1.z, r1.z, l(0), g0.x
and r0.y, r0.y, r1.z
iadd r1.y, r0.y, r0.w
store_raw u0.xy, r0.xx, r1.xy
and r0.x, r0.z, l(2)
if_nz r0.x
imul null, r0.x, cb0[0].y, cb0[0].x
atomic_iadd u0, r0.x, l(1)
endif
endif
ret
