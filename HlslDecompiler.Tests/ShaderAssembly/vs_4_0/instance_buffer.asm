vs_4_0
dcl_globalFlags refactoringAllowed | enableRawAndStructuredBuffers
dcl_constantbuffer CB0[4], immediateIndexed
dcl_resource_structured t0, 64
dcl_input v0
dcl_input v1.xyz
dcl_input_sgv v2.x, instance_id
dcl_output_siv o0, position
dcl_output o1.xyz
dcl_output o2.xyz
dcl_temps 2
ld_structured r0, v2.x, l(48), t0
dp4 r0.w, v0, r0
ld_structured r1, v2.x, l(0), t0
dp4 r0.x, v0, r1
dp3 o1.x, v1.xyz, r1.xyz
ld_structured r1, v2.x, l(16), t0
dp4 r0.y, v0, r1
dp3 o1.y, v1.xyz, r1.xyz
ld_structured r1, v2.x, l(32), t0
dp4 r0.z, v0, r1
dp3 o1.z, v1.xyz, r1.xyz
dp4 o0.x, r0, cb0[0]
dp4 o0.y, r0, cb0[1]
dp4 o0.z, r0, cb0[2]
dp4 o0.w, r0, cb0[3]
mov o2.xyz, r0.xyz
ret
