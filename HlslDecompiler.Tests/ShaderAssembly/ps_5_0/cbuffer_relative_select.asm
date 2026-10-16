ps_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[25], dynamicIndexed
dcl_input_ps linear v0.xy
dcl_input_ps constant v1.x
dcl_output o0
dcl_temps 2
iadd r0.x, v1.x, cb0[24].x
and r0.x, r0.x, l(7)
ilt r0.y, cb0[r0.x + 8].w, l(0)
xor r0.z, r0.x, l(3)
movc r1.w, r0.y, cb0[r0.z + 16].w, cb0[r0.x].w
and r0.y, v1.x, l(1)
movc r1.x, r0.y, cb0[r0.x + 8].x, v0.x
xor r1.y, v0.y, cb0[r0.x + 16].y
utof r1.z, cb0[r0.x + 16].z
add o0, r1, cb0[r0.x + 16]
ret
