cs_4_0
dcl_globalFlags refactoringAllowed
dcl_immediateConstantBuffer { { 1, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } }
dcl_constantbuffer CB0[2], immediateIndexed
dcl_resource_texture2d (float,float,float,float) t0
dcl_uav_structured u0, 16
dcl_input vThreadIDInGroupFlattened
dcl_input vThreadGroupID.xy
dcl_temps 3
dcl_tgsm_structured g0, 16, 64
dcl_thread_group 64, 1, 1
ishl r0.x, vThreadGroupID.x, l(6)
iadd r0.x, r0.x, vThreadIDInGroupFlattened.x
mov r0.y, vThreadGroupID.y
mov r0.zw, l(0, 0, 0, 0)
ld r1, r0, t0
store_structured g0, vThreadIDInGroupFlattened.x, l(0), r1
sync_g_t
mov r1, l(0, 0, 0, 0)
mov r0.y, l(0)
loop
uge r0.z, r0.y, l(4)
breakc_nz r0.z
iadd r0.z, r0.y, vThreadIDInGroupFlattened.x
umin r0.z, r0.z, l(63)
ld_structured r2, r0.z, l(0), g0
dp4 r0.z, cb0[0], icb[r0.y]
mad r1, r2, r0.z, r1
iadd r0.y, r0.y, l(1)
endloop
utof r0.y, cb0[1].x
div r1, r1, r0.y
store_structured u0, r0.x, l(0), r1
ret
