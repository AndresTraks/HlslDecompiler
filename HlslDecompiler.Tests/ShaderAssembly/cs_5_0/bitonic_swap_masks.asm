cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_uav_structured u0, 8
dcl_input vThreadIDInGroupFlattened
dcl_input vThreadID.x
dcl_temps 3
dcl_tgsm_structured g0, 8, 256
dcl_thread_group 256, 1, 1
ld_structured_indexable(structured_buffer, stride=8)(mixed,mixed,mixed,mixed) r0.xy, vThreadID.xx, l(0), u0.xy
store_structured g0.xy, vThreadIDInGroupFlattened.xx, l(0), r0.xy
sync_g_t
ushr r0.x, cb0[0].x, l(1)
and r0.y, vThreadID.x, cb0[0].y
ieq r0.y, r0.y, l(0)
mov r0.z, r0.x
loop
uge r0.w, l(0), r0.z
breakc_nz r0.w
xor r0.w, r0.z, vThreadIDInGroupFlattened.x
ld_structured r1.xy, vThreadIDInGroupFlattened.xx, l(0), g0.xy
ld_structured r1.zw, r0.ww, l(0), g0.xy
ult r2.x, r1.z, r1.x
ult r0.w, vThreadIDInGroupFlattened.x, r0.w
ieq r0.w, r0.w, r0.y
ieq r0.w, r0.w, r2.x
sync_g_t
movc r1.xy, r0.ww, r1.zw, r1.xy
store_structured g0.xy, vThreadIDInGroupFlattened.xx, l(0), r1.xy
sync_g_t
ushr r0.z, r0.z, l(1)
endloop
ld_structured r0.xy, vThreadIDInGroupFlattened.xx, l(0), g0.xy
store_structured u0.xy, vThreadID.xx, l(0), r0.xy
ret
