vs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[4], immediateIndexed
dcl_resource_structured t0, 80
dcl_input v0.xyz
dcl_input_sgv v1.x, instance_id
dcl_output_siv o0, position
dcl_output o1
dcl_temps 3
ld_structured_indexable(structured_buffer, stride=80)(mixed,mixed,mixed,mixed) r0, v1.x, l(0), t0
mov r1.xyz, v0.xyz
mov r1.w, l(1)
dp4 r0.x, r1, r0
ld_structured_indexable(structured_buffer, stride=80)(mixed,mixed,mixed,mixed) r2, v1.x, l(16), t0
dp4 r0.y, r1, r2
ld_structured_indexable(structured_buffer, stride=80)(mixed,mixed,mixed,mixed) r2, v1.x, l(32), t0
dp4 r0.z, r1, r2
ld_structured_indexable(structured_buffer, stride=80)(mixed,mixed,mixed,mixed) r2, v1.x, l(48), t0
dp4 r0.w, r1, r2
dp4 o0.x, r0, cb0[0]
dp4 o0.y, r0, cb0[1]
dp4 o0.z, r0, cb0[2]
dp4 o0.w, r0, cb0[3]
ld_structured_indexable(structured_buffer, stride=80)(mixed,mixed,mixed,mixed) o1, v1.x, l(64), t0
ret
