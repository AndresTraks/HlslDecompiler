cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_texture2d (float,float,float,float) t0
dcl_uav_structured u0, 4
dcl_input vThreadIDInGroupFlattened
dcl_input vThreadIDInGroup.xy
dcl_temps 1
dcl_tgsm_raw g0, 4
dcl_tgsm_raw g1, 4
dcl_thread_group 8, 8, 1
if_z vThreadIDInGroupFlattened.x
store_raw g0.x, l(0), l(2139095039)
store_raw g1.x, l(0), l(0)
endif
sync_g_t
mov r0.xy, vThreadIDInGroup.xy
mov r0.zw, l(0, 0, 0, 0)
ld_indexable(texture2d)(float,float,float,float) r0.x, r0.x, t0.x
atomic_umin g0, l(0), r0.x
atomic_umax g1, l(0), r0.x
sync_g_t
ld_raw r0.x, l(0), g1.x
ld_raw r0.y, l(0), g0.x
add r0.x, -r0.y, r0.x
store_structured u0.x, vThreadIDInGroupFlattened.x, l(0), r0.x
ret
