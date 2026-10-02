cs_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_structured u0, 4
dcl_input vThreadIDInGroup.x
dcl_temps 1
dcl_tgsm_raw g0, 4
dcl_thread_group 64, 1, 1
if_z vThreadIDInGroup.x
store_raw g0.x, l(0), l(0)
endif
sync_g_t
atomic_iadd g0, l(0), vThreadIDInGroup.x
sync_g_t
ld_raw r0.x, l(0), g0.x
store_structured u0.x, vThreadIDInGroup.x, l(0), r0.x
ret
