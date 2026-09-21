cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_texture2d (float,float,float,float) t0
dcl_uav_typed_texture2d (float,float,float,float) u0
dcl_uav_structured u1, 4
dcl_input vThreadIDInGroupFlattened
dcl_input vThreadGroupID.xy
dcl_input vThreadIDInGroup.xy
dcl_temps 2
dcl_tgsm_structured g0, 4, 64
dcl_tgsm_structured g1, 4, 64
dcl_thread_group 8, 8, 1
imad r0.xy, vThreadGroupID.xy, l(8, 8, 0, 0), vThreadIDInGroup.xy
mov r0.zw, l(0, 0, 0, 0)
ld_indexable(texture2d)(float,float,float,float) r1.xyz, r0.xyz, t0.xyz
dp3 r0.z, r1.xyz, l(0.212599993, 0.715200007, 0.0722000003, 0)
store_structured g1.x, vThreadIDInGroupFlattened.x, l(0), r0.z
lt r0.w, cb0[0].z, r0.z
and r0.w, r0.w, l(1)
store_structured g0.x, vThreadIDInGroupFlattened.x, l(0), r0.w
sync_g_t
mov r0.w, l(32)
loop
uge r1.x, l(0), r0.w
breakc_nz r1.x
ult r1.x, vThreadIDInGroupFlattened.x, r0.w
if_nz r1.x
iadd r1.x, r0.w, vThreadIDInGroupFlattened.x
ld_structured r1.y, r1.x, l(0), g1.x
ld_structured r1.z, vThreadIDInGroupFlattened.x, l(0), g1.x
add r1.y, r1.y, r1.z
store_structured g1.x, vThreadIDInGroupFlattened.x, l(0), r1.y
ld_structured r1.x, r1.x, l(0), g0.x
ld_structured r1.y, vThreadIDInGroupFlattened.x, l(0), g0.x
iadd r1.x, r1.x, r1.y
store_structured g0.x, vThreadIDInGroupFlattened.x, l(0), r1.x
endif
sync_g_t
ushr r0.w, r0.w, l(1)
endloop
if_z vThreadIDInGroupFlattened.x
ld_structured r0.w, l(0), l(0), g1.x
mul r0.w, r0.w, l(0.015625)
store_uav_typed u0, vThreadGroupID.xyyy, r0.w
ld_structured r0.w, l(0), l(0), g0.x
umin r1.x, r0.w, l(63)
mov r1.y, l(0)
atomic_iadd u1, r1.xyxx, l(1)
endif
ult r1.xy, r0.xy, cb0[0].xy
and r0.w, r1.y, r1.x
ieq r1.x, cb0[0].w, l(0)
and r0.w, r0.w, r1.x
if_nz r0.w
store_uav_typed u0, r0.xyyy, r0.z
endif
ret
