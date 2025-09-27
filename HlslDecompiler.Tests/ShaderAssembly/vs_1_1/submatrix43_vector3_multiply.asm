vs_1_1
dcl_position v0
mul r0, v0.y, c1
mad r0, c0, v0.x, r0
mad oPos, c2, v0.z, r0
