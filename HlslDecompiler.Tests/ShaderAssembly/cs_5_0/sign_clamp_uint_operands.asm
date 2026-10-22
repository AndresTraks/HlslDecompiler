cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_structured t0, 4
dcl_uav_structured u0, 4
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 8, 1, 1
iadd r0.x, vThreadID.x, l(-4)
ilt r0.y, l(0), r0.x
ilt r0.z, r0.x, l(0)
iadd r0.y, -r0.y, r0.z
imax r0.z, -r0.x, r0.x
imax r0.x, r0.x, l(-2)
imin r0.x, r0.x, cb0[0].y
iadd r0.y, r0.y, r0.z
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.z, vThreadID.x, l(0), t0.x
imax r0.z, -r0.z, r0.z
iadd r0.y, r0.z, r0.y
iadd r0.z, cb0[0].x, l(-3)
ilt r0.w, l(0), r0.z
ilt r0.z, r0.z, l(0)
iadd r0.z, -r0.w, r0.z
iadd r0.y, r0.z, r0.y
iadd r0.x, r0.x, r0.y
store_structured u0.x, vThreadID.x, l(0), r0.x
ret
