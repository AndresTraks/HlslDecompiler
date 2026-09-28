cs_5_0
dcl_globalFlags refactoringAllowed | enableDoublePrecisionFloatOps
dcl_resource_structured t0, 16
dcl_uav_structured u0, 16
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 64, 1, 1
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r0.xy, vThreadID.xx, l(8), t0.xy
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r1, vThreadID.x, l(0), t0.zwxy
dadd r0.xy, r0.xy, r1.zw
dmul r0.zw, r1.xyxy, r1.xyxy
mov r1.xy, r0.zw
mov r1.zw, r0.xy
store_structured u0, vThreadID.x, l(0), r1
ret
