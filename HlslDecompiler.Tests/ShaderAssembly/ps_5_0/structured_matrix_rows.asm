ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
dcl_resource_structured t0, 64
dcl_input_ps linear v0
dcl_output o0
dcl_temps 4
ld_structured_indexable(structured_buffer, stride=64)(mixed,mixed,mixed,mixed) r0, cb0[0].x, l(48), t0
dp4 r0.w, r0, v0
ld_structured_indexable(structured_buffer, stride=64)(mixed,mixed,mixed,mixed) r1, cb0[0].x, l(16), t0
ld_structured_indexable(structured_buffer, stride=64)(mixed,mixed,mixed,mixed) r2, cb0[0].x, l(32), t0
mul r3.xyz, r1.zxy, r2.yzx
mad r3.xyz, r1.yzx, r2.zxy, -r3.xyz
dp4 r0.y, r1, v0
dp4 r0.z, r2, v0
ld_structured_indexable(structured_buffer, stride=64)(mixed,mixed,mixed,mixed) r1, cb0[0].x, l(0), t0
dp3 r2.x, r3.xyz, r1.xyz
dp4 r0.x, r1, v0
add o0, r0, r2.x
ret
