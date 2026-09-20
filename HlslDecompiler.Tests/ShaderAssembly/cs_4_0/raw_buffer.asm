cs_4_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_raw t0
dcl_uav_raw u0
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 64, 1, 1
ult r0.x, vThreadID.x, cb0[0].x
if_nz r0.x
ishl r0.x, vThreadID.x, l(3)
ishl r0.y, vThreadID.x, l(2)
ld_raw r1.xy, r0.xx, t0.xy
iadd r0.x, r1.y, r1.x
store_raw u0.x, r0.y, r0.x
endif
ret
