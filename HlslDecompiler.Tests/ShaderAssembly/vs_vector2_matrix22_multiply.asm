vs_3_0
dcl_position v0
dcl_position o0
dcl_position1 o1.xy
mul r0, c0.xyxy, v0.xyyx
add o0.xz, r0.yy, r0.xy
mul r0, c1.xyxy, v0.xyyx
add o0.yw, r0.xy, r0.xx
mul r0.xy, c0.xy, v0.yx_abs
add o1.x, r0.y, r0.x
mul r0.xy, c1.xy, v0.yx_abs
add o1.y, r0.y, r0.x
