ps_5_0
dcl_globalFlags refactoringAllowed
dcl_resource_structured t0, 36
dcl_input_ps linear v0.w
dcl_input_ps constant v1.x
dcl_output o0
dcl_temps 2
ld_structured_indexable(structured_buffer, stride=36)(mixed,mixed,mixed,mixed) r0.xy, v1.xx, l(28), t0.xy
ilt r0.z, l(0), r0.y
movc o0.w, r0.z, r0.x, v0.w
itof o0.z, r0.y
and r0.x, v1.x, l(1)
ld_structured_indexable(structured_buffer, stride=36)(mixed,mixed,mixed,mixed) r0.yz, v1.xx, l(0), t0.xy
ld_structured_indexable(structured_buffer, stride=36)(mixed,mixed,mixed,mixed) r1.xy, v1.xx, l(16), t0.xy
movc o0.x, r0.x, r1.x, r0.y
xor o0.y, r0.z, r1.y
ret
