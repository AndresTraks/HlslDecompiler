cs_5_0
dcl_globalFlags refactoringAllowed | enableDoublePrecisionFloatOps
dcl_resource_structured t0, 40
dcl_uav_structured u0, 40
dcl_input vThreadID.xy
dcl_temps 3
dcl_thread_group 16, 1, 1
ld_structured_indexable(structured_buffer, stride=40)(mixed,mixed,mixed,mixed) r0.x, vThreadID.x, l(32), t0.x
ftod r0.xy, r0.x
bfi r0.z, l(1), l(4), vThreadID.y, l(0)
iadd r0.w, -r0.z, l(16)
iadd r1.x, r0.w, l(8)
ld_structured_indexable(structured_buffer, stride=40)(mixed,mixed,mixed,mixed) r1.zw, vThreadID.xx, r0.ww, t0.xy
dmov r2.zw, r1.zwzw
ld_structured_indexable(structured_buffer, stride=40)(mixed,mixed,mixed,mixed) r2.xy, vThreadID.xx, r1.xx, t0.xy
dadd r1, r0.xyxy, r2
store_structured u0, vThreadID.x, r0.z, r1
ret
