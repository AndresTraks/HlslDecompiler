cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[2], immediateIndexed
dcl_resource_texture2d (float,float,float,float) t0
dcl_uav_structured u0, 4
dcl_uav_structured u1, 4
dcl_input vThreadIDInGroupFlattened
dcl_input vThreadID.xy
dcl_temps 2
dcl_tgsm_structured g0, 4, 256
dcl_thread_group 16, 16, 1
store_structured g0.x, vThreadIDInGroupFlattened.x, l(0), l(0)
sync_g_t
ult r0.xy, vThreadID.xy, cb0[0].xy
and r0.x, r0.y, r0.x
if_nz r0.x
mov r0.xy, vThreadID.xy
mov r0.zw, l(0, 0, 0, 0)
ld_indexable(texture2d)(float,float,float,float) r0.xyz, r0.xyz, t0.xyz
dp3 r0.x, r0.xyz, l(0.212599993, 0.715200007, 0.0722000003, 0)
lt r0.y, r0.x, l(0.0000999999975)
log r0.x, r0.x
add r0.x, r0.x, -cb0[0].z
mul_sat r0.x, r0.x, cb0[0].w
mad r0.x, r0.x, l(254), l(1)
ftou r0.x, r0.x
movc r0.x, r0.y, l(0), r0.x
mov r0.y, l(0)
atomic_iadd g0, r0.xyxx, l(1)
endif
sync_g_t
ld_structured r0.x, vThreadIDInGroupFlattened.x, l(0), g0.x
mov r1.x, vThreadIDInGroupFlattened.x
mov r1.y, l(0)
atomic_iadd u0, r1.xyxx, r0.x
if_z vThreadIDInGroupFlattened.x
mov r0.xy, l(0, 1, 0, 0)
loop
uge r0.z, r0.y, l(256)
breakc_nz r0.z
ld_structured r0.z, r0.y, l(0), g0.x
utof r0.z, r0.z
utof r0.w, r0.y
mad r0.x, r0.z, r0.w, r0.x
iadd r0.y, r0.y, l(1)
endloop
ld_structured r0.y, l(0), l(0), g0.x
utof r0.y, r0.y
add r0.y, -r0.y, l(256)
max r0.y, r0.y, l(1)
div r0.x, r0.x, r0.y
add r0.x, r0.x, l(-1)
mul r0.x, r0.x, l(0.00393700786)
div r0.x, r0.x, cb0[0].w
add r0.x, r0.x, cb0[0].z
exp r0.x, r0.x
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.y, l(0), l(0), u1.x
add r0.x, -r0.y, r0.x
mad r0.x, cb0[1].x, r0.x, r0.y
store_structured u1.x, l(0), l(0), r0.x
endif
ret
