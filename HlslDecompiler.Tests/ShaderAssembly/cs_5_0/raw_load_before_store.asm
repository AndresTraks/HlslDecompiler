cs_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_raw u0
dcl_input vThreadIDInGroupFlattened
dcl_temps 1
dcl_thread_group 8, 1, 1
ishl r0.x, vThreadIDInGroupFlattened.x, l(2)
ld_raw_indexable(raw_buffer)(mixed,mixed,mixed,mixed) r0.y, r0.x, u0.x
ishl r0.z, r0.y, l(1)
imul null, r0.y, r0.y, l(3)
store_raw u0.x, r0.x, r0.z
iadd r0.x, r0.x, l(64)
store_raw u0.x, r0.x, r0.y
ret
