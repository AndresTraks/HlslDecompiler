cs_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_raw u0
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 64, 1, 1
ishl r0.xy, vThreadID.xx, l(3, 2, 0, 0)
ld_raw_indexable(raw_buffer)(mixed,mixed,mixed,mixed) r0.xz, r0.xx, u0.xy
iadd r0.y, r0.y, l(1024)
ishl r0.x, r0.x, l(16)
or r0.x, r0.z, r0.x
store_raw u0.x, r0.y, r0.x
ret
