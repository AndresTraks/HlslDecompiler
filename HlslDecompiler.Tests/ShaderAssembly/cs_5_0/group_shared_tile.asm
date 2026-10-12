cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 16
dcl_uav_structured u0, 16
dcl_input vThreadIDInGroupFlattened
dcl_input vThreadID.x
dcl_temps 2
dcl_tgsm_raw g0, 16
dcl_tgsm_raw g1, 8
dcl_thread_group 32, 1, 1
if_z vThreadIDInGroupFlattened.x
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r0.xy, l(0), l(0), t0.xy
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r0.z, l(1), l(0), t0.x
mov r0.w, l(1065353216)
store_raw g0, l(0), r0
store_raw g1.xy, l(0), l(3, 0, 0, 0)
endif
sync_g_t
ld_raw r0, l(0), g0
ld_raw r1.xy, l(0), g1.xy
mov r1.zw, l(0, 0, 0, 0)
mad r0, r0, l(2, 2, 2, 2), r1
store_structured u0, vThreadID.x, l(0), r0
ret
