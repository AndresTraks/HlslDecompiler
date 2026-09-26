cs_5_0
dcl_globalFlags refactoringAllowed | enableDoublePrecisionFloatOps
dcl_resource_structured t0, 24
dcl_uav_structured u0, 24
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 64, 1, 1
ld_structured_indexable(structured_buffer, stride=24)(mixed,mixed,mixed,mixed) r0.x, vThreadID.x, l(0), t0.x
add r0.x, r0.x, r0.x
store_structured u0.x, vThreadID.x, l(0), r0.x
ld_structured_indexable(structured_buffer, stride=24)(mixed,mixed,mixed,mixed) r1.xyz, vThreadID.xxx, l(8), t0.xyz
iadd r0.z, r1.z, l(1)
dmul r1.xy, r1.xy, r1.xy
mov r0.xy, r1.xy
store_structured u0.xyz, vThreadID.xxx, l(8), r0.xyz
ret
