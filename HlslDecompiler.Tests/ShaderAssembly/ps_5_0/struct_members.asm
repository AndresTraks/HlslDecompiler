ps_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 24
dcl_uav_structured u1, 24
dcl_input_ps_sgv constant v0.x, primitive_id
dcl_output o0
dcl_temps 1
ld_structured_indexable(structured_buffer, stride=24)(mixed,mixed,mixed,mixed) r0, v0.x, l(0), t0
mad r0, r0.zxyw, l(2, 2, 2, 1), l(0, 0, 0, 1)
store_structured u1, v0.x, l(0), r0
mov o0, r0
ld_structured_indexable(structured_buffer, stride=24)(mixed,mixed,mixed,mixed) r0.xy, v0.xx, l(16), t0.xy
iadd r0.xy, r0.yx, l(3, 3, 0, 0)
store_structured u1.xy, v0.xx, l(16), r0.xy
ret
