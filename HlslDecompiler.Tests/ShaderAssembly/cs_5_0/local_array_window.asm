cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_structured t0, 4
dcl_uav_structured u0, 4
dcl_input vThreadID.x
dcl_temps 1
dcl_indexableTemp x0[8], 4
dcl_thread_group 32, 1, 1
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, vThreadID.x, l(0), t0.x
mov x0[0].x, r0.x
iadd r0, vThreadID.x, l(1, 2, 3, 4)
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, r0.x, l(0), t0.x
mov x0[1].x, r0.x
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, r0.y, l(0), t0.x
mov x0[2].x, r0.x
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, r0.z, l(0), t0.x
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.y, r0.w, l(0), t0.x
mov x0[3].x, r0.x
mov x0[4].x, r0.y
iadd r0.xyz, vThreadID.xxx, l(5, 6, 7, 0)
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, r0.x, l(0), t0.x
mov x0[5].x, r0.x
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, r0.y, l(0), t0.x
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.y, r0.z, l(0), t0.x
mov x0[6].x, r0.x
mov x0[7].x, r0.y
and r0.x, cb0[0].x, l(5)
iadd r0.y, r0.x, l(2)
mov r0.y, x0[r0.y].x
mov r0.z, x0[r0.x + 1].x
mov r0.x, x0[r0.x].x
mul r0.x, r0.x, l(0.5)
add r0.y, r0.y, r0.z
mad r0.x, r0.y, l(0.25), r0.x
store_structured u0.x, vThreadID.x, l(0), r0.x
ret
