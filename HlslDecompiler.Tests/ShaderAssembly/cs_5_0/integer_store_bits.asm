cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 8
dcl_uav_structured u0, 4
dcl_uav_structured u1, 8
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 32, 1, 1
ld_structured_indexable(structured_buffer, stride=8)(mixed,mixed,mixed,mixed) r0.xy, vThreadID.xx, l(0), t0.xy
udiv r0.z, null, r0.x, r0.y
xor r0.xy, r0.xy, l(-2147483648, -2147483648, 0, 0)
store_structured u1.xy, vThreadID.xx, l(0), r0.xy
store_structured u0.x, vThreadID.x, l(0), r0.z
ret
