cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_structured t0, 4
dcl_uav_structured u0, 16
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 64, 1, 1
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, vThreadID.x, l(0), t0.x
xor r0.x, r0.x, cb0[0].x
and r0.y, r0.x, cb0[0].y
countbits r1.xw, r0.xy
firstbit_lo r1.y, r0.x
bfrev r1.z, r0.x
store_structured u0, vThreadID.x, l(0), r1
ret
