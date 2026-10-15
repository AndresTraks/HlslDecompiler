cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 16
dcl_uav_structured u0, 16
dcl_input vThreadIDInGroupFlattened
dcl_input vThreadID.x
dcl_temps 2
dcl_tgsm_structured g0, 20, 64
dcl_thread_group 64, 1, 1
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r0, vThreadID.x, l(0), t0
xor r0.w, r0.w, vThreadIDInGroupFlattened.x
store_structured g0, vThreadIDInGroupFlattened.x, l(0), r0
utof r0.x, vThreadIDInGroupFlattened.x
store_structured g0.x, vThreadIDInGroupFlattened.x, l(16), r0.x
sync_g_t
iadd r0.x, vThreadIDInGroupFlattened.x, l(1)
and r0.x, r0.x, l(63)
ld_structured r0.yzw, r0.xxx, l(0), g0.xyz
ld_structured r1.xw, r0.xx, l(12), g0.yx
mul r1.xyz, r0.yzw, r1.xxx
store_structured u0, vThreadID.x, l(0), r1
ret
