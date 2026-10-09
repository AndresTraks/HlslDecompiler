cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_uav_structured u0, 20
dcl_input vThreadID.x
dcl_temps 3
dcl_thread_group 64, 1, 1
ld_structured_indexable(structured_buffer, stride=20)(mixed,mixed,mixed,mixed) r0, vThreadID.x, l(0), u0
ld_structured_indexable(structured_buffer, stride=20)(mixed,mixed,mixed,mixed) r1.x, vThreadID.x, l(16), u0.x
mul r1.y, r0.w, cb0[0].x
mad r2.xyz, r1.yyy, l(0.5, -9.80000019, 0.25, 0), r0.xyz
add r2.w, r0.w, -cb0[0].x
store_structured u0, vThreadID.x, l(0), r2
lt r0.x, r2.w, l(0)
or r0.y, r1.x, l(0x00000001)
movc r0.x, r0.x, r0.y, r1.x
store_structured u0.x, vThreadID.x, l(16), r0.x
ret
