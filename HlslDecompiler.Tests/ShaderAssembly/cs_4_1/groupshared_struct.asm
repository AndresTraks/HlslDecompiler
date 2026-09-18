cs_4_1
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 16
dcl_uav_structured u0, 16
dcl_input vThreadIDInGroupFlattened
dcl_input vThreadID.x
dcl_temps 3
dcl_tgsm_structured g0, 16, 64
dcl_thread_group 64, 1, 1
ld_structured r0, vThreadID.x, l(0), t0
store_structured g0, vThreadIDInGroupFlattened.x, l(0), r0
sync_g_t
mov r1.w, l(0)
mov r0, l(0, 0, 0, 0)
loop
uge r2.x, r0.w, l(4)
breakc_nz r2.x
iadd r2.x, r0.w, vThreadIDInGroupFlattened.x
and r2.x, r2.x, l(63)
ld_structured r2, r2.x, l(0), g0
mad r0.xyz, r2.xyz, r2.www, r0.xyz
add r1.w, r1.w, r2.w
iadd r0.w, r0.w, l(1)
endloop
max r0.w, r1.w, l(0.0000999999975)
div r1.xyz, r0.xyz, r0.www
store_structured u0, vThreadID.x, l(0), r1
ret
