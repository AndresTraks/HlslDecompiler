vs_5_0
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[33], dynamicIndexed
dcl_input v0
dcl_output_siv o0, position
dcl_temps 2
iadd r0.x, cb0[32].x, l(1)
bfi r0.x, l(3), l(2), r0.x, l(0)
ishl r0.y, cb0[32].x, l(2)
dp4 r1.x, v0, cb0[r0.y]
dp4 r1.y, v0, cb0[r0.y + 1]
dp4 r1.z, v0, cb0[r0.y + 2]
dp4 r1.w, v0, cb0[r0.y + 3]
dp4 o0.x, r1, cb0[r0.x]
dp4 o0.y, r1, cb0[r0.x + 1]
dp4 o0.z, r1, cb0[r0.x + 2]
dp4 o0.w, r1, cb0[r0.x + 3]
ret
