cs_4_1
dcl_globalFlags refactoringAllowed
dcl_constantbuffer cb0[1], immediateIndexed
dcl_resource_structured t0, 4
dcl_uav_structured u0, 4
dcl_input vThreadIDInGroupFlattened
dcl_input vThreadID.x
dcl_temps 1
dcl_tgsm_structured g0, 4, 64
dcl_thread_group 64, 1, 1
ld_structured r0.x, vThreadID.x, l(0), t0.x
store_structured g0.x, vThreadIDInGroupFlattened.x, l(0), r0.x
sync_g_t
imul null, r0.x, vThreadIDInGroupFlattened.x, cb0[0].x
ushr r0.y, vThreadIDInGroupFlattened.x, l(2)
xor r0.x, r0.y, r0.x
mov r0.yz, l(0, 0, 0, 0)
loop
uge r0.w, r0.z, l(4)
breakc_nz r0.w
iadd r0.w, r0.z, r0.x
and r0.w, r0.w, l(63)
ld_structured r0.w, r0.w, l(0), g0.x
add r0.y, r0.w, r0.y
iadd r0.z, r0.z, l(1)
endloop
mul r0.x, r0.y, l(0.25)
store_structured u0.x, vThreadID.x, l(0), r0.x
ret
