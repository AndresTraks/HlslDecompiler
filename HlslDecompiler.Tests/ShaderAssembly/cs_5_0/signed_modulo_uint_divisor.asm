cs_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_structured u0, 4
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 8, 1, 1
bufinfo_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, u0.x
iadd r0.y, vThreadID.x, l(-4)
imax r0.xz, r0.xy, -r0.xy
udiv null, r0.x, r0.z, r0.x
ushr r0.z, r0.z, l(2)
ineg r0.w, r0.x
and r1.x, r0.y, l(-2147483648)
xor r0.y, r0.y, l(4)
and r0.y, r0.y, l(-2147483648)
movc r0.x, r1.x, r0.w, r0.x
ineg r0.w, r0.z
movc r0.y, r0.y, r0.w, r0.z
iadd r0.x, r0.y, r0.x
store_structured u0.x, vThreadID.x, l(0), r0.x
ret
