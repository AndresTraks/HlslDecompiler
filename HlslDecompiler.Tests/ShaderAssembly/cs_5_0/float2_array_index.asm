cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 36
dcl_uav_structured u0, 36
dcl_input vThreadID.xy
dcl_temps 1
dcl_thread_group 16, 1, 1
ld_structured_indexable(structured_buffer, stride=36)(mixed,mixed,mixed,mixed) r0.x, vThreadID.x, l(32), t0.x
bfi r0.y, l(2), l(3), vThreadID.y, l(0)
ld_structured_indexable(structured_buffer, stride=36)(mixed,mixed,mixed,mixed) r0.zw, vThreadID.xx, r0.yy, t0.xy
mul r0.xz, r0.xx, r0.wz
store_structured u0.xy, vThreadID.xx, r0.yy, r0.xz
ret
