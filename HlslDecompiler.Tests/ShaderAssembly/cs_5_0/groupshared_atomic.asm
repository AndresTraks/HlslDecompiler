cs_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_structured u0, 4
dcl_input vThreadGroupID.x
dcl_input vThreadIDInGroup.x
dcl_temps 2
dcl_tgsm_structured g0, 4, 64
dcl_thread_group 64, 1, 1
store_structured g0.x, vThreadIDInGroup.x, l(0), l(0)
sync_g_t
and r0, vThreadIDInGroup.x, l(3, 31, 15, 7)
ishl r0.z, l(1), r0.z
mov r1.yw, l(0, 0, 0, 0)
mov r1.xz, r0.yw
atomic_or g0, r1.xyxx, r0.z
atomic_and g0, r1.zwzz, l(65535)
mov r0.y, l(0)
atomic_cmp_store g0, r0.xyxx, l(0), vThreadGroupID.x
sync_g_t
ld_structured r0.x, vThreadIDInGroup.x, l(0), g0.x
store_structured u0.x, vThreadIDInGroup.x, l(0), r0.x
ret
