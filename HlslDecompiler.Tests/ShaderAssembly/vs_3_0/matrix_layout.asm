vs_3_0
dcl_position v0
dcl_position o0
mul r0, c1, v0.y
mad r0, v0.x, c0, r0
mad r0, v0.z, c2, r0
mad r0, v0.w, c3, r0
dp4 o0.x, r0, c4
dp4 o0.y, r0, c5
dp4 o0.z, r0, c6
dp4 o0.w, r0, c7
