vs_3_0
dcl_position v0
dcl_position o0
mul r0, c1, v0.y
mad r0, c0, v0.x, r0
mad o0, c2, v0.z, r0
