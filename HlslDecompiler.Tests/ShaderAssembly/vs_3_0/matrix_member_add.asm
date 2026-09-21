vs_3_0
dcl_position v0
dcl_position o0
mul r0, c1, v0.y
mad r0, v0.x, c0, r0
mad r0, v0.z, c2, r0
mad r0, v0.w, c3, r0
add o0, r0, c4
