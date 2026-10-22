cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_structured t0, 4
dcl_uav_structured u0, 4
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 8, 1, 1
iadd r0.x, vThreadID.x, l(-4)
itof r0.y, r0.x
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.z, vThreadID.x, l(0), t0.x
itof r0.w, r0.z
utof r0.xz, r0.xz
add r0.y, r0.w, r0.y
add r0.y, r0.z, r0.y
iadd r0.z, cb0[0].x, l(-9)
itof r0.z, r0.z
add r0.y, r0.z, r0.y
add r0.x, r0.x, r0.y
store_structured u0.x, vThreadID.x, l(0), r0.x
ret
