cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 48
dcl_uav_structured u0, 48
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 8, 1, 1
ld_structured_indexable(structured_buffer, stride=48)(mixed,mixed,mixed,mixed) r0.xyz, vThreadID.xxx, l(0), t0.xyz
ld_structured_indexable(structured_buffer, stride=48)(mixed,mixed,mixed,mixed) r1, vThreadID.x, l(28), t0.yzwx
mov r0.w, r1.w
store_structured u0, vThreadID.x, l(0), r0.zyxw
ld_structured_indexable(structured_buffer, stride=48)(mixed,mixed,mixed,mixed) r0.x, vThreadID.x, l(44), t0.x
iadd r0.x, r0.x, l(1)
utof r1.w, r0.x
store_structured u0, vThreadID.x, l(16), r1
mov r1.w, vThreadID.x
store_structured u0, vThreadID.x, l(32), r1
ret
