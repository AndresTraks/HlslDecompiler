cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_texture2d (float,float,float,float) t0
dcl_uav_typed_texture2d (float,float,float,float) u0
dcl_input vThreadIDInGroup.x
dcl_input vThreadID.xy
dcl_temps 4
dcl_tgsm_structured g0, 16, 72
dcl_thread_group 64, 1, 1
iadd r0, vThreadIDInGroup.x, l(4, 3, 5, 2)
mov r1.yz, vThreadID.xy
mov r1.w, l(0)
ld_indexable(texture2d)(float,float,float,float) r2, r1.yzww, t0
store_structured g0, r0.x, l(0), r2
ult r2.x, vThreadIDInGroup.x, l(4)
if_nz r2.x
iadd r1.xy, vThreadID.xx, l(-4, 64, 0, 0)
imax r1.x, r1.x, l(0)
ld_indexable(texture2d)(float,float,float,float) r2, r1.xzww, t0
store_structured g0, vThreadIDInGroup.x, l(0), r2
iadd r1.x, vThreadIDInGroup.x, l(68)
ld_indexable(texture2d)(float,float,float,float) r2, r1.yzww, t0
store_structured g0, r1.x, l(0), r2
endif
sync_g_t
ld_structured r1, r0.x, l(0), g0
ld_structured r2, r0.y, l(0), g0
ld_structured r3, r0.z, l(0), g0
add r2, r2, r3
mul r2, r2, l(0.194000006, 0.194000006, 0.194000006, 0.194000006)
mad r1, r1, l(0.226999998, 0.226999998, 0.226999998, 0.226999998), r2
ld_structured r0, r0.w, l(0), g0
iadd r2, vThreadIDInGroup.x, l(6, 1, 7, 8)
ld_structured r3, r2.x, l(0), g0
add r0, r0, r3
mad r0, r0, l(0.120999999, 0.120999999, 0.120999999, 0.120999999), r1
ld_structured r1, r2.y, l(0), g0
ld_structured r3, r2.z, l(0), g0
add r1, r1, r3
mad r0, r1, l(0.0540000014, 0.0540000014, 0.0540000014, 0.0540000014), r0
ld_structured r1, vThreadIDInGroup.x, l(0), g0
ld_structured r2, r2.w, l(0), g0
add r1, r1, r2
mad r0, r1, l(0.0160000008, 0.0160000008, 0.0160000008, 0.0160000008), r0
store_uav_typed u0, vThreadID.xyyy, r0
ret
