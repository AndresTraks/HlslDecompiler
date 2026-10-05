cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_uav_structured u0, 4
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 256, 1, 1
xor r0.x, vThreadID.x, cb0[0].x
ult r0.y, vThreadID.x, r0.x
if_nz r0.y
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.y, vThreadID.x, l(0), u0.x
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.z, r0.x, l(0), u0.x
and r0.w, vThreadID.x, cb0[0].y
ieq r0.w, r0.w, l(0)
ult r1.x, r0.z, r0.y
ieq r0.w, r0.w, r1.x
if_nz r0.w
store_structured u0.x, vThreadID.x, l(0), r0.z
store_structured u0.x, r0.x, l(0), r0.y
endif
endif
ret
