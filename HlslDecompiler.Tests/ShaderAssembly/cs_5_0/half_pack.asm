cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_structured t0, 8
dcl_uav_structured u0, 4
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 64, 1, 1
ld_structured_indexable(structured_buffer, stride=8)(mixed,mixed,mixed,mixed) r0.xy, vThreadID.xx, l(0), t0.xy
mul r0.xy, r0.xy, cb0[0].xx
f32tof16 r0.xy, r0.xy
imad r0.x, r0.y, l(65536), r0.x
store_structured u0.x, vThreadID.x, l(0), r0.x
ret
