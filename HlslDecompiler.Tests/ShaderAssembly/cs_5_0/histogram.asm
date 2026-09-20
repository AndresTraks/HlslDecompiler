cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_structured t0, 4
dcl_uav_raw u0
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 64, 1, 1
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, vThreadID.x, l(0), t0.x
mul r0.x, r0.x, cb0[0].x
ftou r0.x, r0.x
umin r0.x, r0.x, l(15)
ishl r0.y, r0.x, l(2)
atomic_iadd u0, r0.y, l(1)
atomic_umax u0, l(64), r0.x
ret
