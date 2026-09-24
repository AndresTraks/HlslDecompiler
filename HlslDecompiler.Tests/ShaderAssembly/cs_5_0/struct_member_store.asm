cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 24
dcl_uav_structured u0, 24
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 8, 1, 1
ld_structured_indexable(structured_buffer, stride=24)(mixed,mixed,mixed,mixed) r0.xyz, vThreadID.xxx, l(12), t0.xyz
utof r0.yz, r0.zy
add r1.x, r0.x, r0.x
add r0.xy, r0.yz, l(3, 3, 0, 0)
ftou r1.yz, r0.xy
store_structured u0.xyz, vThreadID.xxx, l(12), r1.xyz
ret
