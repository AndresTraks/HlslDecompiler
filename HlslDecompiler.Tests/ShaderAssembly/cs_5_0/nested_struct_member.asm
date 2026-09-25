cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 32
dcl_uav_structured u0, 32
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 8, 1, 1
ld_structured_indexable(structured_buffer, stride=32)(mixed,mixed,mixed,mixed) r0, vThreadID.x, l(0), t0
utof r0.w, r0.w
add r1.xyz, r0.zyx, r0.zyx
add r0.x, r0.w, l(1)
ftou r1.w, r0.x
store_structured u0, vThreadID.x, l(0), r1
ld_structured_indexable(structured_buffer, stride=32)(mixed,mixed,mixed,mixed) r0.xy, vThreadID.xx, l(24), t0.xy
store_structured u0, vThreadID.x, l(16), r0.xyxy
ret
