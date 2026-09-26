vs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[4], immediateIndexed
dcl_resource_structured t0, 64
dcl_input v0.xyz
dcl_input_sgv v1.x, instance_id
dcl_output_siv o0, position
dcl_output o1
dcl_temps 1
ld_structured_indexable(structured_buffer, stride=64)(mixed,mixed,mixed,mixed) r0.xyz, v1.xxx, l(52), t0.xyz
add r0.xyz, r0.xyz, v0.xyz
mov r0.w, l(1)
dp4 o0.x, r0, cb0[0]
dp4 o0.y, r0, cb0[1]
dp4 o0.z, r0, cb0[2]
dp4 o0.w, r0, cb0[3]
ld_structured_indexable(structured_buffer, stride=64)(mixed,mixed,mixed,mixed) o1, v1.x, l(36), t0
ret
