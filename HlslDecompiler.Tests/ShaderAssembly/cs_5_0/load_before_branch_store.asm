cs_5_0
dcl_globalFlags refactoringAllowed
dcl_uav_structured u0, 4
dcl_input vThreadIDInGroupFlattened
dcl_temps 1
dcl_thread_group 8, 1, 1
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, vThreadIDInGroupFlattened.x, l(0), u0.x
and r0.y, vThreadIDInGroupFlattened.x, l(1)
if_nz r0.y
store_structured u0.x, vThreadIDInGroupFlattened.x, l(0), l(0.000000)
endif
iadd r0.y, vThreadIDInGroupFlattened.x, l(8)
store_structured u0.x, r0.y, l(0), r0.x
ret
