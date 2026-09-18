cs_4_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer cb0[1], immediateIndexed
dcl_resource_structured t0, 4
dcl_uav_structured u0, 4
dcl_input vThreadIDInGroupFlattened
dcl_input vThreadID.x
dcl_temps 2
dcl_tgsm_structured g0, 4, 64
dcl_thread_group 64, 1, 1
ld_structured r0.x, vThreadID.x, l(0), t0.x
ushr r0.y, r0.x, l(16)
xor r0.x, r0.y, r0.x
store_structured g0.x, vThreadIDInGroupFlattened.x, l(0), r0.x
sync_g_t
mov r0.xy, l(0, 0, 0, 0)
loop
uge r0.z, r0.y, l(8)
breakc_nz r0.z
iadd r0.z, r0.y, vThreadIDInGroupFlattened.x
and r0.z, r0.z, l(63)
ld_structured r1.x, r0.z, l(0), g0.x
and r0.z, r1.x, l(255)
iadd r0.x, r0.z, r0.x
iadd r0.y, r0.y, l(1)
endloop
utof r0.x, r0.x
utof r0.y, cb0[0].x
div r0.x, r0.x, r0.y
store_structured u0.x, vThreadID.x, l(0), r0.x
ret
