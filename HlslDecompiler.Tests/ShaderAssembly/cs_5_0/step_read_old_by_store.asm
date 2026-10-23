cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_uav_structured u0, 4
dcl_uav_structured u1, 4
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 8, 1, 1
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, vThreadID.x, l(0), u0.x
iadd r0.x, r0.x, -cb0[0].x
iadd r0.y, vThreadID.x, cb0[0].y
ushr r0.z, r0.y, l(1)
store_structured u1.x, vThreadID.x, l(0), r0.z
ult r0.z, r0.y, r0.x
umin r0.x, r0.x, r0.y
and r0.y, r0.z, l(1)
iadd r0.x, r0.y, r0.x
store_structured u0.x, vThreadID.x, l(0), r0.x
ret
