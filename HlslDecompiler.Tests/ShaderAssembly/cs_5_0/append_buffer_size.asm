cs_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_structured u0, 16
dcl_uav_structured u1, 4
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 8, 1, 1
bufinfo_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r0.x, u0.x
iadd r0.x, r0.x, l(16)
utof r0.x, r0.x
store_structured u1.x, vThreadID.x, l(0), r0.x
ret
