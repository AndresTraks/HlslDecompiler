cs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_structured t0, 4
dcl_uav_structured u0, 4
dcl_input vThreadIDInGroupFlattened
dcl_temps 1
dcl_thread_group 64, 1, 1
ld_structured_indexable(structured_buffer, stride=4)(mixed,mixed,mixed,mixed) r0.x, vThreadIDInGroupFlattened.x, l(0), t0.x
lt r0.x, cb0[0].x, r0.x
if_nz r0.x
imad r0.x, vThreadIDInGroupFlattened.x, l(3), l(1)
store_structured u0.x, r0.x, l(0), r0.x
else
imad r0.xy, vThreadIDInGroupFlattened.xx, l(5, 5, 0, 0), l(2, 3, 0, 0)
store_structured u0.x, r0.x, l(0), r0.y
endif
store_structured u0.x, vThreadIDInGroupFlattened.x, l(0), r0.x
ret
