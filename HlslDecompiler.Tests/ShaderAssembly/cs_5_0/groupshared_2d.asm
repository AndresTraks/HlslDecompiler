cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_texture2d (float,float,float,float) t0
dcl_uav_typed_texture2d (float,float,float,float) u0
dcl_input vThreadIDInGroup.xy
dcl_input vThreadID.xy
dcl_temps 1
dcl_tgsm_structured g0, 64, 8
dcl_thread_group 8, 8, 1
mov r0.xy, vThreadID.xy
mov r0.zw, l(0, 0, 0, 0)
ld_indexable(texture2d)(float,float,float,float) r0.xy, r0.xy, t0.xy
ishl r0.z, vThreadIDInGroup.x, l(3)
store_structured g0.xy, vThreadIDInGroup.yy, r0.zz, r0.xy
sync_g_t
iadd r0.xy, vThreadIDInGroup.xy, l(1, 1, 0, 0)
and r0.y, r0.y, l(7)
bfi r0.x, l(3), l(3), r0.x, l(0)
ld_structured r0.xw, vThreadIDInGroup.yy, r0.xx, g0.xy
ld_structured r0.yz, r0.yy, r0.zz, g0.xy
add r0.xy, r0.yz, r0.xw
xor r0.z, r0.x, vThreadIDInGroup.x
mov r0.w, l(1)
store_uav_typed u0, vThreadID.xyyy, r0
ret
