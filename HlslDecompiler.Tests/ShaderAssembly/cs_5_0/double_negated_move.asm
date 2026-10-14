cs_5_0
dcl_globalFlags refactoringAllowed | enableDoublePrecisionFloatOps
dcl_resource_structured t0, 16
dcl_uav_structured u0, 16
dcl_uav_structured u1, 8
dcl_input vThreadID.x
dcl_temps 3
dcl_thread_group 32, 1, 1
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r0, vThreadID.x, l(0), t0
dlt r1.x, -r0.xy, r0.zw
dmovc r1.xy, r1.x, -r0.xy, r0.zw
dmin r1.zw, r0.xyxy, r1.xyxy
dmov r1.zw, -r1.zwzw
mov r2.zw, r1.zw
dmax r1.zw, -r0.xyxy, r1.xyxy
dne r1.x, r0.xy, r1.xy
deq r0.x, r0.zw, r0.xy
and r0.x, r0.x, l(0x00000001)
and r0.y, r1.x, l(0x00000001)
store_structured u1.xy, vThreadID.xx, l(0), r0.xy
mov r2.xy, r1.zw
store_structured u0, vThreadID.x, l(0), r2
ret
