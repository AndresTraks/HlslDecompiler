cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 16
dcl_uav_structured u0, 16
dcl_input vThreadIDInGroupFlattened
dcl_input vThreadID.x
dcl_temps 1
dcl_tgsm_raw g0, 16
dcl_thread_group 32, 1, 1
if_z vThreadIDInGroupFlattened.x
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r0.xy, l(0), l(0), t0.xy
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r0.z, l(1), l(0), t0.x
mov r0.w, l(1065353217)
store_raw g0, l(0), r0
endif
sync_g_t
ld_raw r0, l(0), g0
add r0, r0, r0
store_structured u0, vThreadID.x, l(0), r0
ret
