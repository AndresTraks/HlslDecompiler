vs_4_0
dcl_globalFlags refactoringAllowed enableRawAndStructuredBuffers
dcl_constantbuffer cb0[4], immediateIndexed
dcl_resource_structured t0, 32
dcl_input_sgv v0.x, vertex_id
dcl_output_siv o0, position
dcl_output o1.xy
dcl_output o1.z
dcl_temps 3
and r0.xy, v0.xx, l(1, 2, 0, 0)
movc r0.xy, r0.xy, l(0.000000, 0.000000, 0, 0), l(NaN, NaN, 0, 0)
itof r0.xy, r0.xy
ushr r0.z, v0.x, l(2)
ld_structured r1.x, r0.z, l(28), t0.x
ld_structured r2, r0.z, l(0), t0
mul r0.xy, r0.xy, r1.xx
mov r0.z, l(0)
add r1.xyz, r0.xyz, r2.xyz
mov_sat r0.w, r2.w
mov o1.xyz, r0.xyw
mov r1.w, l(1)
dp4 o0.x, r1, cb0[0]
dp4 o0.y, r1, cb0[1]
dp4 o0.z, r1, cb0[2]
dp4 o0.w, r1, cb0[3]
ret
