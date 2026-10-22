cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_structured t0, 4
dcl_uav_structured u0, 4
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 8, 1, 1
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, vThreadID.x, l(0), t0.x
iadd r0.y, vThreadID.x, l(-4)
xor r0.z, r0.x, r0.y
imax r0.xw, r0.xy, -r0.xy
and r0.yz, r0.yz, l(0, -2147483648, -2147483648, 0)
udiv r0.x, null, r0.w, r0.x
ineg r1.x, r0.x
movc r0.x, r0.z, r1.x, r0.x
imax r1.xy, cb0[0].xy, -cb0[0].xy
udiv null, r0.z, r0.w, r1.x
ineg r0.w, r0.z
movc r0.y, r0.y, r0.w, r0.z
iadd r0.x, r0.x, r0.y
udiv null, r0.y, vThreadID.x, r1.y
iadd r0.x, r0.y, r0.x
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.y, l(0), l(0), t0.x
iadd r0.y, r0.y, l(-7)
imax r0.z, r0.y, -r0.y
xor r0.y, r0.y, cb0[0].x
and r0.y, r0.y, l(-2147483648)
udiv r0.z, null, r0.z, r1.x
ineg r0.w, r0.z
movc r0.y, r0.y, r0.w, r0.z
iadd r0.x, r0.y, r0.x
store_structured u0.x, vThreadID.x, l(0), r0.x
ret
