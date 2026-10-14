cs_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_structured u0, 12
dcl_input vThreadIDInGroupFlattened
dcl_input vThreadID.x
dcl_temps 1
dcl_tgsm_structured g0, 4, 3
dcl_thread_group 32, 1, 1
if_z vThreadIDInGroupFlattened.x
store_structured g0.x, l(0), l(0), l(3)
store_structured g0.x, l(1), l(0), l(1065353216)
store_structured g0.x, l(2), l(0), l(2139095041)
endif
sync_g_t
ld_structured r0.x, l(0), l(0), g0.x
iadd r0.x, r0.x, vThreadID.x
ld_structured r0.w, l(1), l(0), g0.x
imul null, r0.y, r0.w, vThreadID.x
ld_structured r0.w, l(2), l(0), g0.x
xor r0.z, r0.w, vThreadID.x
store_structured u0.xyz, vThreadID.xxx, l(0), r0.xyz
ret
