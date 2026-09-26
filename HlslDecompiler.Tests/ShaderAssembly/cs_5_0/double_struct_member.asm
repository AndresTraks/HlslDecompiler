cs_5_0
dcl_globalFlags refactoringAllowed | enableDoublePrecisionFloatOps | enable11_1DoubleExtensions
dcl_resource_structured t0, 16
dcl_uav_structured u0, 16
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 64, 1, 1
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r0, vThreadID.x, l(0), t0.zwxy
dadd r1.xy, -r0.zw, r0.xy
ddiv r0.xy, r0.xy, r0.zw
mov r0.zw, r0.xy
mov r0.xy, r1.xy
store_structured u0, vThreadID.x, l(0), r0
ret
