cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_raw t0
dcl_uav_raw u0
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 8, 1, 1
ishl r0.xy, vThreadID.xx, l(2, 4, 0, 0)
ld_raw_indexable(raw_buffer)(mixed,mixed,mixed,mixed) r1, r0.y, t0
store_raw u0.x, r0.x, r1.w
imul null, r0.x, vThreadID.x, l(12)
store_raw u0.xyz, r0.xxx, r1.xyz
ret
