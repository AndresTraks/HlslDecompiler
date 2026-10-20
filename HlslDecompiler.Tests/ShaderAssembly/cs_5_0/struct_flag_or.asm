cs_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_structured u0, 32
dcl_input vThreadID.x
dcl_temps 3
dcl_thread_group 32, 1, 1
ld_structured_indexable(structured_buffer, stride=32)(mixed,mixed,mixed,mixed) r0, vThreadID.x, l(0), u0
ld_structured_indexable(structured_buffer, stride=32)(mixed,mixed,mixed,mixed) r1, vThreadID.x, l(16), u0
mad r2.xyz, r1.xyz, l(0.0160000008, 0.0160000008, 0.0160000008, 0), r0.xyz
add r2.w, r0.w, l(-0.0160000008)
store_structured u0, vThreadID.x, l(0), r2
lt r0.x, r2.w, l(0)
and r0.x, r0.x, l(1)
or r0.x, r0.x, r1.w
store_structured u0.x, vThreadID.x, l(28), r0.x
ret
