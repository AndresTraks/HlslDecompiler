cs_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 8
dcl_uav_structured u0, 4
dcl_input vThreadID.x
dcl_temps 1
dcl_thread_group 64, 1, 1
ld_structured_indexable(structured_buffer, stride=8)(mixed,mixed,mixed,mixed) r0.xy, vThreadID.xx, l(0), t0.xy
dp2 r0.z, r0.xy, l(0.5, 0.25, 0, 0)
sqrt r0.x, r0.x
dp2 r0.x, r0.xx, r0.zz
add_sat r0.x, -r0.y, r0.x
store_structured u0.x, vThreadID.x, l(0), r0.x
ret
