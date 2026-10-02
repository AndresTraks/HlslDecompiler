cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_structured t0, 4
dcl_uav_structured u0, 4
dcl_input vThreadIDInGroupFlattened
dcl_temps 2
dcl_tgsm_raw g0, 4
dcl_tgsm_structured g1, 4, 64
dcl_thread_group 64, 1, 1
if_z vThreadIDInGroupFlattened.x
store_raw g0.x, l(0), l(0)
endif
sync_g_t
mov r0.x, vThreadIDInGroupFlattened.x
loop
uge r0.y, r0.x, cb0[0].x
breakc_nz r0.y
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.y, r0.x, l(0), t0.x
lt r0.y, r0.y, cb0[0].y
if_nz r0.y
iadd r0.y, r0.x, l(64)
mov r0.x, r0.y
continue
endif
imm_atomic_iadd r1.x, g0, l(0), l(1)
ult r0.y, r1.x, l(64)
if_nz r0.y
store_structured g1.x, r1.x, l(0), r0.x
endif
iadd r0.x, r0.x, l(64)
endloop
sync_g_t
ld_structured r0.x, vThreadIDInGroupFlattened.x, l(0), g1.x
store_structured u0.x, vThreadIDInGroupFlattened.x, l(0), r0.x
ret
