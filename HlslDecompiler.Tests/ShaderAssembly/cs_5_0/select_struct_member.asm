cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 16
dcl_uav_structured u0, 8
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 32, 1, 1
ld_structured_indexable(structured_buffer, stride=16)(mixed,mixed,mixed,mixed) r0, vThreadID.x, l(0), t0.xwyz
mul r0.w, r0.w, l(100)
ftou r0.w, r0.w
lt r1.x, l(0.5), r0.x
movc r0.x, r1.x, r0.z, r0.w
store_structured u0.xy, vThreadID.xx, l(0), r0.xy
ret
