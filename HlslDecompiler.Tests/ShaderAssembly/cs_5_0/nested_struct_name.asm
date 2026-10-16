cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 68
dcl_uav_structured u0, 12
dcl_input vThreadID.x
dcl_temps 4
dcl_thread_group 32, 1, 1
ld_structured_indexable(structured_buffer, stride=68)(mixed,mixed,mixed,mixed) r0, vThreadID.x, l(0), t0
ult r0.x, l(0), r0.x
ld_structured_indexable(structured_buffer, stride=68)(mixed,mixed,mixed,mixed) r1, vThreadID.x, l(16), t0
mul r0.yzw, r0.yzw, r1.xxx
and r0.xyz, r0.yzw, r0.xxx
ld_structured_indexable(structured_buffer, stride=68)(mixed,mixed,mixed,mixed) r2, vThreadID.x, l(32), t0
mad r1.xyz, r1.yzw, r2.xxx, r0.xyz
ld_structured_indexable(structured_buffer, stride=68)(mixed,mixed,mixed,mixed) r0.w, vThreadID.x, l(0), t0.x
ult r3.xyz, l(1, 2, 3, 0), r0.www
movc r0.xyz, r3.xxx, r1.xyz, r0.xyz
ld_structured_indexable(structured_buffer, stride=68)(mixed,mixed,mixed,mixed) r1, vThreadID.x, l(48), t0
mad r2.xyz, r2.yzw, r1.xxx, r0.xyz
movc r0.xyz, r3.yyy, r2.xyz, r0.xyz
ld_structured_indexable(structured_buffer, stride=68)(mixed,mixed,mixed,mixed) r0.w, vThreadID.x, l(64), t0.x
mad r1.xyz, r1.yzw, r0.www, r0.xyz
movc r0.xyz, r3.zzz, r1.xyz, r0.xyz
store_structured u0.xyz, vThreadID.xxx, l(0), r0.xyz
ret
