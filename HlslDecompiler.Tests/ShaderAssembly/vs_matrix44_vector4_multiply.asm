vs_3_0
def c4, 5, 2, 3, 4
dcl_position v0
dcl_position o0
dcl_position1 o1
dcl_position2 o2
dcl_position3 o3
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
mul r0, c4, v0
mul r1, r0.y, c1
mad r1, c0, r0.x, r1
mad r1, c2, r0.z, r1
mad o3, c3, r0.w, r1
