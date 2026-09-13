cs_4_1
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 4
dcl_uav_structured u0, 4
dcl_input vThreadIDInGroupFlattened
dcl_input vThreadGroupID.x
dcl_temps 1
dcl_tgsm_structured g0, 4, 64
dcl_thread_group 64, 1, 1
ishl r0.x, vThreadGroupID.x, l(6)
iadd r0.x, r0.x, vThreadIDInGroupFlattened.x
ld_structured r0.x, r0.x, l(0), t0.x
store_structured g0.x, vThreadIDInGroupFlattened.x, l(0), r0.x
sync_g_t
mov r0.x, l(32)
loop
uge r0.y, l(0), r0.x
breakc_nz r0.y
ult r0.y, vThreadIDInGroupFlattened.x, r0.x
if_nz r0.y
iadd r0.y, r0.x, vThreadIDInGroupFlattened.x
ld_structured r0.y, r0.y, l(0), g0.x
ld_structured r0.z, vThreadIDInGroupFlattened.x, l(0), g0.x
add r0.y, r0.y, r0.z
store_structured g0.x, vThreadIDInGroupFlattened.x, l(0), r0.y
endif
sync_g_t
ushr r0.x, r0.x, l(1)
endloop
if_z vThreadIDInGroupFlattened.x
ld_structured r0.x, l(0), l(0), g0.x
store_structured u0.x, vThreadGroupID.x, l(0), r0.x
endif
ret
