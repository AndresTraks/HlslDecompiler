vs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[4], immediateIndexed
dcl_resource_structured t0, 116
dcl_input v0.xyz
dcl_input v1.xyz
dcl_input_sgv v2.x, instance_id
dcl_output_siv o0, position
dcl_output o1.xyz
dcl_output o2
dcl_temps 7
ld_structured_indexable(structured_buffer, stride=116)(mixed,mixed,mixed,mixed) r0, v2.x, l(36), t0
mov r1.x, r0.x
ld_structured_indexable(structured_buffer, stride=116)(mixed,mixed,mixed,mixed) r2, v2.x, l(52), t0.xzyw
mov r1.y, r2.x
ld_structured_indexable(structured_buffer, stride=116)(mixed,mixed,mixed,mixed) r3, v2.x, l(68), t0.xywz
mov r1.z, r3.x
ld_structured_indexable(structured_buffer, stride=116)(mixed,mixed,mixed,mixed) r4, v2.x, l(84), t0
mov r1.w, r4.x
mov r5.xyz, v0.xyz
mov r5.w, l(1)
dp4 r1.x, r5, r1
mov r6.x, r0.y
mov r6.y, r2.z
mov r6.z, r3.y
mov r6.w, r4.y
dp4 r1.y, r5, r6
mov r2.x, r0.z
mov r3.x, r0.w
mov r3.y, r2.w
mov r2.z, r3.w
mov r2.w, r4.z
mov r3.w, r4.w
dp4 r1.w, r5, r3
dp4 r1.z, r5, r2
dp4 o0.x, r1, cb0[0]
dp4 o0.y, r1, cb0[1]
dp4 o0.z, r1, cb0[2]
dp4 o0.w, r1, cb0[3]
ld_structured_indexable(structured_buffer, stride=116)(mixed,mixed,mixed,mixed) r0.xyz, v2.xxx, l(0), t0.xyz
dp3 o1.x, v1.xyz, r0.xyz
ld_structured_indexable(structured_buffer, stride=116)(mixed,mixed,mixed,mixed) r0.xyz, v2.xxx, l(12), t0.xyz
dp3 o1.y, v1.xyz, r0.xyz
ld_structured_indexable(structured_buffer, stride=116)(mixed,mixed,mixed,mixed) r0.xyz, v2.xxx, l(24), t0.xyz
dp3 o1.z, v1.xyz, r0.xyz
ld_structured_indexable(structured_buffer, stride=116)(mixed,mixed,mixed,mixed) o2, v2.x, l(100), t0
ret
