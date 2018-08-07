vs_3_0
dcl_position v0
dcl_position o0
dcl_position1 o1
dcl_position2 o2
mul r0, c1, v0.y
mad r0, c0, v0.x, r0
mad r0, c2, v0.z, r0
mad o0, c3, v0.w, r0
mul r0, c1, v0.x
mad r0, c0, v0.y, r0
mad r0, c2, v0.z, r0
mad o1, c3, v0.w, r0
mul r0, c1, v0.x_abs
mad r0, c0, v0.y_abs, r0
mad r0, c2, v0.z_abs, r0
mad o2, c3, v0.w_abs, r0
