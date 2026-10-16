cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_buffer (uint,uint,uint,uint) t0
dcl_uav_structured u0, 8
dcl_input vThreadGroupID.x
dcl_input vThreadIDInGroup.x
dcl_temps 2
dcl_tgsm_structured g0, 12, 8
dcl_thread_group 64, 1, 1
ult r0.x, vThreadIDInGroup.x, l(8)
if_nz r0.x
store_structured g0.xy, vThreadIDInGroup.xx, l(0), l(0, 0, 0, 0)
endif
sync_g_t
imad r0.y, vThreadGroupID.x, l(64), vThreadIDInGroup.x
ld_indexable(buffer)(uint,uint,uint,uint) r0.y, r0.y, t0.x
and r1.x, r0.y, l(7)
mov r1.yz, l(0, 0, 4, 0)
atomic_iadd g0, r1.xyxx, l(1)
atomic_umax g0, r1.xzxx, r0.y
sync_g_t
if_nz r0.x
ishl r0.x, vThreadGroupID.x, l(3)
iadd r0.x, r0.x, vThreadIDInGroup.x
ld_structured r0.yz, vThreadIDInGroup.xx, l(0), g0.xy
store_structured u0.xy, r0.xx, l(0), r0.yz
endif
ret
