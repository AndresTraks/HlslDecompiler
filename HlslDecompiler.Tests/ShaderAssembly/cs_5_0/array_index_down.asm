cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 24
dcl_uav_structured u0, 24
dcl_input vThreadID.xy
dcl_temps 1
dcl_thread_group 16, 1, 1
ld_structured_indexable(structured_buffer, stride=24)(mixed,mixed,mixed,mixed) r0.x, vThreadID.x, l(0), t0.x
ftoi r0.x, r0.x
udiv null, r0.y, vThreadID.y, l(5)
ishl r0.y, r0.y, l(2)
iadd r0.z, -r0.y, l(20)
iadd r0.y, r0.y, l(4)
ld_structured_indexable(structured_buffer, stride=24)(mixed,mixed,mixed,mixed) r0.z, vThreadID.x, r0.z, t0.x
iadd r0.x, r0.x, r0.z
store_structured u0.x, vThreadID.x, r0.y, r0.x
ret
