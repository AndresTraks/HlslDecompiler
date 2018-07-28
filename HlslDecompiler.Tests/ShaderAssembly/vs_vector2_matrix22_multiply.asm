vs_3_0
def c2, 1, 2, 0, 0
dcl_position v0
dcl_position o0
mul r0.xy, c0.xy, v0.yx
add o0.x, r0.y, r0.x
mul r0.xy, c1.xy, v0.yx
add o0.y, r0.y, r0.x
mov o0.zw, c2.xy
