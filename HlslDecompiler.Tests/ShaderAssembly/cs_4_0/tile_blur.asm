cs_4_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer cb0[1], immediateIndexed
dcl_resource_structured t0, 16
dcl_uav_structured u0, 16
dcl_input vThreadIDInGroupFlattened
dcl_input vThreadGroupID.xy
dcl_input vThreadIDInGroup.xy
dcl_temps 3
dcl_tgsm_structured g0, 16, 64
dcl_thread_group 8, 8, 1
ishl r0.xy, vThreadGroupID.xy, l(3)
iadd r0.xy, r0.xy, vThreadIDInGroup.xy
imad r0.x, r0.y, cb0[0].x, r0.x
ld_structured r1, r0.x, l(0), t0
store_structured g0, vThreadIDInGroupFlattened.x, l(0), r1
sync_g_t
if_nz vThreadIDInGroup.x
iadd r0.y, vThreadIDInGroupFlattened.x, l(-1)
ld_structured r2, r0.y, l(0), g0
add r1, r1, r2
endif
ult r0.y, vThreadIDInGroup.x, l(7)
if_nz r0.y
iadd r0.y, vThreadIDInGroupFlattened.x, l(1)
ld_structured r2, r0.y, l(0), g0
add r1, r1, r2
endif
mul r1, r1, l(0.333333343, 0.333333343, 0.333333343, 0.333333343)
store_structured u0, r0.x, l(0), r1
ret
