cs_5_0
dcl_globalFlags refactoringAllowed | enableDoublePrecisionFloatOps
dcl_resource_structured t0, 32
dcl_uav_structured u0, 32
dcl_input vThreadID.xy
dcl_temps 2
dcl_thread_group 16, 1, 1
udiv null, r0.x, vThreadID.y, l(3)
ishl r0.x, r0.x, l(3)
iadd r0.y, -r0.x, l(24)
iadd r0.x, r0.x, l(8)
ld_structured_indexable(structured_buffer, stride=32)(mixed,mixed,mixed,mixed) r0.zw, vThreadID.xx, r0.yy, t0.xy
dmul r0.zw, r0.zwzw, d(0.000000l, 2.000000l)
ld_structured_indexable(structured_buffer, stride=32)(mixed,mixed,mixed,mixed) r0.y, vThreadID.x, l(0), t0.x
ftod r1.xy, r0.y
dadd r0.zw, r0.zwzw, r1.xyxy
mov r0.yz, r0.zw
store_structured u0.xy, vThreadID.xx, r0.xx, r0.yz
ret
