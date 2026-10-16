cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 4
dcl_uav_structured u0, 8
dcl_input vThreadID.x
dcl_temps 2
dcl_thread_group 64, 1, 1
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, vThreadID.x, l(0), t0.x
and r0.y, r0.x, l(2139095040)
iadd r0.y, r0.y, l(-1056964608)
ne r0.z, r0.x, l(0)
bfi r0.x, l(23), l(0), r0.x, l(1056964608)
and r1.x, r0.z, r0.x
and r0.x, r0.z, r0.y
ishr r0.x, r0.x, l(23)
itof r1.y, r0.x
store_structured u0.xy, vThreadID.xx, l(0), r1.xy
ret
