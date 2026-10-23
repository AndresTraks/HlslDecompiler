cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_uav_raw u0
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 8, 1, 1
ishl r0.x, vThreadID.x, l(2)
iadd r0.x, r0.x, l(16)
ld_raw_indexable(raw_buffer)(mixed,mixed,mixed,mixed) r0.x, r0.x, u0.x
ilt r0.y, r0.x, cb0[0].x
movc r0.x, r0.y, r0.x, l(9)
atomic_iadd u0, l(4), r0.x
ret
