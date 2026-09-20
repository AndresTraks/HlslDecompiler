cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_structured t0, 4
dcl_uav_structured u0, 4
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 64, 1, 1
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, vThreadID.x, l(0), t0.x
firstbit_shi r0.y, r0.x
iadd r0.x, r0.x, cb0[0].x
ieq r0.z, r0.y, l(-1)
iadd r0.y, -r0.y, l(31)
movc r0.y, r0.z, l(-1), r0.y
firstbit_hi r0.z, r0.x
iadd r0.z, -r0.z, l(31)
movc r0.x, r0.x, r0.z, l(-1)
iadd r0.x, r0.x, r0.y
store_structured u0.x, vThreadID.x, l(0), r0.x
ret
